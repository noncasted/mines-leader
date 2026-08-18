#!/usr/bin/env python3
"""Suggest proven-safe opens and flags. Does not touch the live match.

  python3 tools/scripts/board-solver.py                  # last MCP observation
  python3 tools/scripts/board-solver.py --from-agent
  python3 tools/scripts/board-solver.py path/to/obs.json
  python3 tools/scripts/board-solver.py < obs.json

Reads SharedAgentObservation JSON (or {Self:{Cells|BoardAscii}}).
Uses only player-visible info — ignores HasMine even if oracle leaked it.
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

NEIGHBORS = ((-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1))
MAX_ENUM = 18
SCRIPT_DIR = Path(__file__).resolve().parent


def main() -> int:
    parser = argparse.ArgumentParser(description="Minesweeper suggestions. Never opens cells.")
    parser.add_argument("source", nargs="?", help="Observation JSON file, or omit for stdin/--from-agent")
    parser.add_argument("--from-agent", action="store_true", help="Pull game_get_state via game-agent.py")
    parser.add_argument("--json", action="store_true", help="JSON only")
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--side", choices=("self", "opponent"), default="self")
    args = parser.parse_args()

    if args.self_test:
        return 0 if _self_test() else 1

    observation = _load_observation(args.source, args.from_agent)
    result = solve(observation, side=args.side)
    if args.json:
        json.dump(result, sys.stdout, indent=2, ensure_ascii=False)
        sys.stdout.write("\n")
    else:
        _print_human(result)
    return 0


def solve(observation: dict, side: str = "self") -> dict:
    player = _player(observation, side)
    cells = _index_cells(player)
    width, height = _board_size(cells, player.get("BoardAscii") or "")
    constraints = _constraints(cells, width, height)

    mines: dict[tuple[int, int], str] = {}
    safe: dict[tuple[int, int], str] = {}
    _apply_level1(constraints, mines, safe)
    _apply_level2(constraints, mines, safe)
    _apply_enumeration(constraints, mines, safe)

    chords = _chords(cells, width, height, mines, safe)
    opens = [
        {"x": x, "y": y, "kind": "single", "reason": reason}
        for (x, y), reason in sorted(safe.items())
    ]
    for chord in chords:
        opens.append(chord)

    guesses = _guesses(constraints, cells, width, height, mines, safe)

    return {
        "width": width,
        "height": height,
        "safe_opens": [item for item in opens if item["kind"] == "single"],
        "chords": [item for item in opens if item["kind"] == "chord"],
        "flags": [
            {"x": x, "y": y, "reason": reason}
            for (x, y), reason in sorted(mines.items())
        ],
        "guesses": guesses,
        "closed": sum(1 for cell in cells.values() if cell["status"] == "closed"),
        "flagged": sum(1 for cell in cells.values() if cell["status"] == "flagged"),
    }


def _load_observation(source: str | None, from_agent: bool) -> dict:
    if from_agent or source == "--from-agent":
        return _from_agent()
    if source:
        return json.loads(Path(source).read_text())
    if sys.stdin.isatty():
        return _from_agent()
    return json.loads(sys.stdin.read())


def _from_agent() -> dict:
    command = [sys.executable, str(SCRIPT_DIR / "game-agent.py"), "state"]
    completed = subprocess.run(command, check=False, capture_output=True, text=True)
    if completed.returncode != 0:
        raise SystemExit(completed.stderr.strip() or "game-agent.py state failed")
    return json.loads(completed.stdout)


def _player(observation: dict, side: str) -> dict:
    if side == "opponent":
        return observation.get("Opponent") or observation.get("opponent") or {}
    return (
        observation.get("Self")
        or observation.get("self")
        or observation
    )


def _index_cells(player: dict) -> dict[tuple[int, int], dict]:
    raw = player.get("Cells") or player.get("cells") or []
    if raw:
        grid = {}
        for cell in raw:
            x = int(cell.get("X", cell.get("x")))
            y = int(cell.get("Y", cell.get("y")))
            status = (cell.get("Status") or cell.get("status") or "closed").lower()
            if status == "exploded":
                status = "open"
            grid[(x, y)] = {
                "status": status,
                "mines": int(cell.get("MinesAround") or cell.get("minesAround") or 0),
            }
        return grid

    ascii_board = player.get("BoardAscii") or player.get("boardAscii") or ""
    return _parse_ascii(ascii_board)


def _parse_ascii(text: str) -> dict[tuple[int, int], dict]:
    grid: dict[tuple[int, int], dict] = {}
    rows = [line for line in text.splitlines() if line.strip() != ""]
    for y, row in enumerate(rows):
        for x, glyph in enumerate(row.rstrip("\n")):
            if glyph in {".", "~", "#"}:
                grid[(x, y)] = {"status": "closed", "mines": 0}
            elif glyph in {"F", "f"}:
                grid[(x, y)] = {"status": "flagged", "mines": 0}
            elif glyph == "*":
                grid[(x, y)] = {"status": "open", "mines": 0}
            elif glyph.isdigit():
                grid[(x, y)] = {"status": "open", "mines": int(glyph)}
    return grid


def _board_size(cells: dict[tuple[int, int], dict], ascii_board: str) -> tuple[int, int]:
    if cells:
        return max(x for x, _ in cells) + 1, max(y for _, y in cells) + 1
    rows = [line for line in ascii_board.splitlines() if line]
    if not rows:
        return 16, 16
    return max(len(row) for row in rows), len(rows)


def _neighbors(x: int, y: int, width: int, height: int):
    for dx, dy in NEIGHBORS:
        nx, ny = x + dx, y + dy
        if 0 <= nx < width and 0 <= ny < height:
            yield nx, ny


def _constraints(
    cells: dict[tuple[int, int], dict],
    width: int,
    height: int,
) -> list[tuple[tuple[int, int], int, list[tuple[int, int]]]]:
    result = []
    for (x, y), cell in cells.items():
        if cell["status"] != "open" or cell["mines"] < 0:
            continue
        flagged = 0
        unknown: list[tuple[int, int]] = []
        for nx, ny in _neighbors(x, y, width, height):
            neighbor = cells.get((nx, ny))
            if neighbor is None:
                continue
            if neighbor["status"] == "flagged":
                flagged += 1
            elif neighbor["status"] == "closed":
                unknown.append((nx, ny))
        remaining = cell["mines"] - flagged
        if remaining < 0 or remaining > len(unknown):
            continue
        if unknown:
            result.append(((x, y), remaining, unknown))
    return result


def _apply_level1(
    constraints: list[tuple[tuple[int, int], int, list[tuple[int, int]]]],
    mines: dict[tuple[int, int], str],
    safe: dict[tuple[int, int], str],
) -> None:
    changed = True
    while changed:
        changed = False
        for origin, remaining, unknown in constraints:
            live = [cell for cell in unknown if cell not in mines and cell not in safe]
            mines_hit = sum(1 for cell in unknown if cell in mines)
            need = remaining - mines_hit
            if need < 0 or need > len(live):
                continue
            if need == 0:
                for cell in live:
                    if cell not in safe:
                        safe[cell] = f"level1 around {origin[0]},{origin[1]}: all mines already flagged"
                        changed = True
            elif need == len(live):
                for cell in live:
                    if cell not in mines:
                        mines[cell] = f"level1 around {origin[0]},{origin[1]}: remaining closed == remaining mines"
                        changed = True


def _apply_level2(
    constraints: list[tuple[tuple[int, int], int, list[tuple[int, int]]]],
    mines: dict[tuple[int, int], str],
    safe: dict[tuple[int, int], str],
) -> None:
    for (origin_a, rem_a, unk_a), (origin_b, rem_b, unk_b) in _pairs(constraints):
        set_a, set_b = set(unk_a), set(unk_b)
        if not set_b or not set_b.issubset(set_a) or set_a == set_b:
            continue
        diff = [cell for cell in set_a - set_b if cell not in mines and cell not in safe]
        if not diff:
            continue
        mines_a = sum(1 for cell in set_a if cell in mines)
        mines_b = sum(1 for cell in set_b if cell in mines)
        need = (rem_a - mines_a) - (rem_b - mines_b)
        if need == 0:
            for cell in diff:
                safe[cell] = f"subset {origin_a[0]},{origin_a[1]} \\ {origin_b[0]},{origin_b[1]}"
        elif need == len(diff):
            for cell in diff:
                mines[cell] = f"subset mines {origin_a[0]},{origin_a[1]} \\ {origin_b[0]},{origin_b[1]}"


def _pairs(items):
    for i, left in enumerate(items):
        for right in items[i + 1 :]:
            yield left, right
            yield right, left


def _apply_enumeration(
    constraints: list[tuple[tuple[int, int], int, list[tuple[int, int]]]],
    mines: dict[tuple[int, int], str],
    safe: dict[tuple[int, int], str],
) -> None:
    frontier: list[tuple[int, int]] = []
    seen: set[tuple[int, int]] = set()
    for _, remaining, unknown in constraints:
        for cell in unknown:
            if cell in mines or cell in safe or cell in seen:
                continue
            seen.add(cell)
            frontier.append(cell)
    if not frontier or len(frontier) > MAX_ENUM:
        return

    index = {cell: i for i, cell in enumerate(frontier)}
    clauses = []
    for _, remaining, unknown in constraints:
        bits = [index[cell] for cell in unknown if cell in index]
        known_mines = sum(1 for cell in unknown if cell in mines)
        need = remaining - known_mines
        if bits:
            clauses.append((bits, need))

    total = 1 << len(frontier)
    mine_count = [0] * len(frontier)
    valid = 0
    for mask in range(total):
        if not _mask_ok(mask, clauses):
            continue
        valid += 1
        for i in range(len(frontier)):
            if mask & (1 << i):
                mine_count[i] += 1

    if valid == 0:
        return
    for i, cell in enumerate(frontier):
        if mine_count[i] == valid:
            mines[cell] = f"enum: mine in all {valid} solutions"
        elif mine_count[i] == 0:
            safe[cell] = f"enum: safe in all {valid} solutions"


def _mask_ok(mask: int, clauses: list[tuple[list[int], int]]) -> bool:
    for bits, need in clauses:
        got = 0
        for bit in bits:
            if mask & (1 << bit):
                got += 1
        if got != need:
            return False
    return True


def _chords(
    cells: dict[tuple[int, int], dict],
    width: int,
    height: int,
    mines: dict[tuple[int, int], str],
    safe: dict[tuple[int, int], str],
) -> list[dict]:
    result = []
    for (x, y), cell in cells.items():
        if cell["status"] != "open" or cell["mines"] <= 0:
            continue
        flagged = 0
        closed = []
        for nx, ny in _neighbors(x, y, width, height):
            neighbor = cells.get((nx, ny))
            if neighbor is None:
                continue
            if neighbor["status"] == "flagged" or (nx, ny) in mines:
                flagged += 1
            elif neighbor["status"] == "closed" and (nx, ny) not in mines:
                closed.append((nx, ny))
        if flagged == cell["mines"] and closed:
            result.append(
                {
                    "x": x,
                    "y": y,
                    "kind": "chord",
                    "opens": [{"x": cx, "y": cy} for cx, cy in closed],
                    "reason": f"chord {x},{y}: flags satisfy number, {len(closed)} closed neighbors",
                }
            )
    return result


def _guesses(
    constraints: list[tuple[tuple[int, int], int, list[tuple[int, int]]]],
    cells: dict[tuple[int, int], dict],
    width: int,
    height: int,
    mines: dict[tuple[int, int], str],
    safe: dict[tuple[int, int], str],
) -> list[dict]:
    local: dict[tuple[int, int], list[float]] = defaultdict(list)
    for origin, remaining, unknown in constraints:
        live = [cell for cell in unknown if cell not in mines and cell not in safe]
        mines_hit = sum(1 for cell in unknown if cell in mines)
        need = remaining - mines_hit
        if not live or need <= 0:
            continue
        probability = need / len(live)
        for cell in live:
            local[cell].append(probability)

    ranked = []
    for cell, values in local.items():
        if cell in mines or cell in safe:
            continue
        p_mine = max(values)
        ranked.append(
            {
                "x": cell[0],
                "y": cell[1],
                "p_mine": round(p_mine, 3),
                "reason": f"local max p_mine={p_mine:.2f} from {len(values)} clue(s)",
            }
        )
    ranked.sort(key=lambda item: (item["p_mine"], item["x"], item["y"]))
    return ranked[:12]


def _print_human(result: dict) -> None:
    print(f"board {result['width']}x{result['height']}  closed={result['closed']}  flagged={result['flagged']}")
    if result["safe_opens"]:
        print("SAFE OPENS")
        for item in result["safe_opens"]:
            print(f"  {item['x']},{item['y']}  {item['reason']}")
    else:
        print("SAFE OPENS  (none proven)")
    if result["chords"]:
        print("CHORDS")
        for item in result["chords"]:
            cells = " ".join(f"{c['x']},{c['y']}" for c in item["opens"])
            print(f"  chord {item['x']},{item['y']} -> {cells}")
    if result["flags"]:
        print("FLAGS")
        for item in result["flags"]:
            print(f"  {item['x']},{item['y']}  {item['reason']}")
    if not result["safe_opens"] and result["guesses"]:
        print("GUESSES (lowest local mine chance, not proven)")
        for item in result["guesses"][:6]:
            print(f"  {item['x']},{item['y']}  p_mine={item['p_mine']:.2f}  {item['reason']}")


def _self_test() -> bool:
    #  .1F
    #  ...
    # clue 1 at (1,0) already has its mine at (2,0) → remaining closed are safe
    ascii_board = ".1F\n..."
    result = solve({"Self": {"BoardAscii": ascii_board}})
    opens = {(item["x"], item["y"]) for item in result["safe_opens"]}
    if opens != {(0, 0), (0, 1), (1, 1), (2, 1)}:
        print("self-test safe failed", opens, file=sys.stderr)
        return False

    # 1.  → the single closed neighbor of a 1 is a mine
    result = solve(
        {
            "Self": {
                "Cells": [
                    {"X": 0, "Y": 0, "Status": "open", "MinesAround": 1},
                    {"X": 1, "Y": 0, "Status": "closed"},
                ]
            }
        }
    )
    flags = {(item["x"], item["y"]) for item in result["flags"]}
    if flags != {(1, 0)}:
        print("self-test flag failed", flags, file=sys.stderr)
        return False
    print("self-test ok")
    return True


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except BrokenPipeError:
        raise SystemExit(0)
