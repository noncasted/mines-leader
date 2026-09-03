#!/usr/bin/env python3
"""Suggest proven-safe opens and flags. Does not touch the live match.

  python3 tools/scripts/board-solver.py                  # last MCP observation
  python3 tools/scripts/board-solver.py --from-agent
  python3 tools/scripts/board-solver.py path/to/obs.json
  python3 tools/scripts/board-solver.py < obs.json

Reads SharedAgentObservation JSON (or {Self:{Cells|BoardAscii}}).
Uses only player-visible info — ignores HasMine even if oracle leaked it.

Besides safe opens / flags it suggests card targets:
  scout_targets   own board, pattern centers with the most unknown closed cells
  attack_targets  opponent board (when generated), pattern centers ranked by
                  closed cells minus proven mines (metric "closed") and by
                  open cells (metric "open", for Trebuchet / MineCluster / CarpetBomb)
Default patterns: Rhombus 4, Cross 2, Line 5. Override with --shape / --size.
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
DEFAULT_SIZES = {"Rhombus": 4, "Cross": 2, "Line": 5}
DEFAULT_SHAPES = tuple(DEFAULT_SIZES.items())
TARGETS_PER_SHAPE = 3
# ZipZap (config.cards.json): needs an OPEN cell in Rhombus(Size) around the click, then
# defuses unflagged mines found in Rhombus(SearchRadius) around the click.
ZIPZAP_SIZE = 3
ZIPZAP_SEARCH = 4


def main() -> int:
    parser = argparse.ArgumentParser(description="Minesweeper suggestions. Never opens cells.")
    parser.add_argument("source", nargs="?", help="Observation JSON file, or omit for stdin/--from-agent")
    parser.add_argument("--from-agent", action="store_true", help="Pull game_get_state via game-agent.py")
    parser.add_argument("--json", action="store_true", help="JSON only")
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--side", choices=("self", "opponent"), default="self")
    parser.add_argument("--shape", choices=("Rhombus", "Cross", "Line"), help="Only this card pattern for targets")
    parser.add_argument("--size", type=int, help="Pattern size for --shape")
    args = parser.parse_args()

    if args.self_test:
        return 0 if _self_test() else 1

    shapes = DEFAULT_SHAPES
    if args.shape:
        shapes = ((args.shape, args.size or DEFAULT_SIZES[args.shape]),)

    observation = _load_observation(args.source, args.from_agent)
    result = solve(observation, side=args.side, shapes=shapes)
    if args.json:
        json.dump(result, sys.stdout, indent=2, ensure_ascii=False)
        sys.stdout.write("\n")
    else:
        _print_human(result)
    return 0


def solve(
    observation: dict,
    side: str = "self",
    shapes: tuple[tuple[str, int], ...] = DEFAULT_SHAPES,
    limit: int = TARGETS_PER_SHAPE,
) -> dict:
    player = _player(observation, side)
    cells = _index_cells(player)
    width, height = _board_size(cells, player.get("BoardAscii") or "")
    constraints = _constraints(cells, width, height)

    mines: dict[tuple[int, int], str] = {}
    safe: dict[tuple[int, int], str] = {}
    _apply_level1(constraints, mines, safe)
    _apply_level2(constraints, mines, safe)
    _apply_enumeration(constraints, mines, safe)

    scout_targets = _scout_targets(cells, width, height, mines, safe, shapes, limit)
    attack_targets = _attack_targets(observation, shapes, limit)
    zipzap_targets = _zipzap_targets(cells, width, height, mines, safe, limit)

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
        "generated": bool(cells),
        "safe_opens": [item for item in opens if item["kind"] == "single"],
        "chords": [item for item in opens if item["kind"] == "chord"],
        "flags": [
            {"x": x, "y": y, "reason": reason}
            for (x, y), reason in sorted(mines.items())
        ],
        "guesses": guesses,
        "closed": sum(1 for cell in cells.values() if cell["status"] == "closed"),
        "flagged": sum(1 for cell in cells.values() if cell["status"] == "flagged"),
        "scout_targets": scout_targets,
        "attack_targets": attack_targets,
        "zipzap_targets": zipzap_targets,
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
    command = [sys.executable, str(SCRIPT_DIR / "game-agent.py"), "--json", "state"]
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


def _rhombus_grid(size: int) -> list[list[bool]]:
    # Mirrors Shared.RhombusShape: even sizes are built as size + 2 and trimmed by one ring.
    is_even = size % 2 == 0
    if is_even:
        size += 2
    center = (size - 1) / 2.0
    grid = [[abs(i - center) + abs(j - center) <= center for j in range(size)] for i in range(size)]
    if is_even:
        grid = [row[1:-1] for row in grid[1:-1]]
    return grid


def _line_grid(length: int, horizontal: bool) -> list[list[bool]]:
    center = length // 2
    return [
        [(y == center) if horizontal else (x == center) for x in range(length)]
        for y in range(length)
    ]


def _cross_grid(size: int) -> list[list[bool]]:
    size = 2 * size - 1
    center = size // 2
    return [[x == center or y == center for x in range(size)] for y in range(size)]


def _pattern_grids(shape: str, size: int) -> list[tuple[str, list[list[bool]]]]:
    if shape == "Rhombus":
        return [("", _rhombus_grid(size))]
    if shape == "Cross":
        return [("", _cross_grid(size))]
    if shape == "Line":
        return [("horizontal", _line_grid(size, True)), ("vertical", _line_grid(size, False))]
    raise ValueError(f"unknown shape {shape}")


def _pattern_cells(grid: list[list[bool]], center: tuple[int, int]) -> list[tuple[int, int]]:
    # Mirrors CellExtensions.Select: start = center - size / 2, then walk the grid.
    size = len(grid)
    half = size // 2
    start_x, start_y = center[0] - half, center[1] - half
    return [
        (start_x + x, start_y + y)
        for y in range(size)
        for x in range(size)
        if grid[y][x]
    ]


def _rank_targets(
    shape: str,
    size: int,
    width: int,
    height: int,
    score: "callable",
    limit: int = TARGETS_PER_SHAPE,
) -> list[dict]:
    """Best centers for one shape. Line cards pick the orientation with more hits,
    so a Line center scores by its better orientation."""
    ranked = []
    for y in range(height):
        for x in range(width):
            best = None
            for label, grid in _pattern_grids(shape, size):
                value = score(_pattern_cells(grid, (x, y)))
                if best is None or value > best[0]:
                    best = (value, label)
            if best is None or best[0] <= 0:
                continue
            ranked.append((best[0], x, y, best[1]))
    ranked.sort(key=lambda item: (-item[0], item[1], item[2]))
    return [
        {"x": x, "y": y, "shape": shape, "size": size, "value": value, "orientation": label}
        for value, x, y, label in ranked[:limit]
    ]


def _scout_targets(
    cells: dict[tuple[int, int], dict],
    width: int,
    height: int,
    mines: dict[tuple[int, int], str],
    safe: dict[tuple[int, int], str],
    shapes: tuple[tuple[str, int], ...],
    limit: int = TARGETS_PER_SHAPE,
) -> list[dict]:
    """Own-board centers whose pattern covers the most unknown closed cells:
    closed, not flagged, not already proven mine or proven safe."""
    unknown = {
        cell
        for cell, info in cells.items()
        if info["status"] == "closed" and cell not in mines and cell not in safe
    }
    result = []
    for shape, size in shapes:
        for item in _rank_targets(shape, size, width, height, lambda area: sum(1 for c in area if c in unknown), limit):
            item["unknown"] = item.pop("value")
            orientation = f" {item['orientation']}" if item["orientation"] else ""
            item["reason"] = f"{item['unknown']} unknown closed cells in {shape.lower()} {size}{orientation}"
            item.pop("orientation")
            result.append(item)
    result.sort(key=lambda item: (-item["unknown"], item["x"], item["y"]))
    return result


def _zipzap_targets(
    cells: dict[tuple[int, int], dict],
    width: int,
    height: int,
    mines: dict[tuple[int, int], str],
    safe: dict[tuple[int, int], str],
    limit: int = TARGETS_PER_SHAPE,
) -> list[dict]:
    """Own-board centers for ZipZap: an open cell inside Rhombus(ZIPZAP_SIZE) around the
    click, ranked by unknown closed cells inside Rhombus(ZIPZAP_SEARCH) — that is where
    the card looks for unflagged mines. Proven mines count too: ZipZap defuses them."""
    opened = {cell for cell, info in cells.items() if info["status"] == "open"}
    if not opened:
        return []
    candidates = {
        cell
        for cell, info in cells.items()
        if info["status"] == "closed" and cell not in safe
    }
    trigger = _rhombus_grid(ZIPZAP_SIZE)
    search = _rhombus_grid(ZIPZAP_SEARCH)
    ranked = []
    for y in range(height):
        for x in range(width):
            if not any(c in opened for c in _pattern_cells(trigger, (x, y))):
                continue
            value = sum(1 for c in _pattern_cells(search, (x, y)) if c in candidates)
            if value <= 0:
                continue
            ranked.append((value, x, y))
    ranked.sort(key=lambda item: (-item[0], item[1], item[2]))
    return [
        {
            "x": x,
            "y": y,
            "shape": "ZipZap",
            "size": ZIPZAP_SIZE,
            "unknown": value,
            "reason": f"{value} closed cells in search rhombus {ZIPZAP_SEARCH}, open cell in rhombus {ZIPZAP_SIZE}",
        }
        for value, x, y in ranked[:limit]
    ]


def _attack_targets(
    observation: dict,
    shapes: tuple[tuple[str, int], ...],
    limit: int = TARGETS_PER_SHAPE,
) -> list[dict]:
    """Opponent-board centers. Metric "closed": closed cells minus mines the solver
    proves from the opponent numbers (a mine is already there, planting is wasted).
    Metric "open": open cells, for cards that re-close and mine open cells
    (Trebuchet, MineCluster, CarpetBomb, FortuneBlast, FogOfWar)."""
    opponent = observation.get("Opponent") or observation.get("opponent") or {}
    cells = _index_cells(opponent)
    if not cells:
        return []

    width, height = _board_size(cells, opponent.get("BoardAscii") or "")
    constraints = _constraints(cells, width, height)
    mines: dict[tuple[int, int], str] = {}
    safe: dict[tuple[int, int], str] = {}
    _apply_level1(constraints, mines, safe)
    _apply_level2(constraints, mines, safe)
    _apply_enumeration(constraints, mines, safe)

    closed = {cell for cell, info in cells.items() if info["status"] == "closed" and cell not in mines}
    opened = {cell for cell, info in cells.items() if info["status"] == "open"}

    result = []
    for shape, size in shapes:
        for metric, pool, note in (
            ("closed", closed, "closed cells without proven opponent mines"),
            ("open", opened, "open cells (Trebuchet / MineCluster / CarpetBomb need open cells)"),
        ):
            for item in _rank_targets(shape, size, width, height, lambda area, pool=pool: sum(1 for c in area if c in pool), limit):
                value = item.pop("value")
                item[metric] = value
                item["metric"] = metric
                orientation = f" {item['orientation']}" if item["orientation"] else ""
                item["reason"] = f"{value} {note} in {shape.lower()} {size}{orientation}"
                item.pop("orientation")
                result.append(item)
    result.sort(key=lambda item: (item["metric"], -item.get(item["metric"], 0), item["x"], item["y"]))
    return result


HUMAN_CAP = 12


def _print_human(result: dict) -> None:
    print("\n".join(format_human(result)))


def format_human(result: dict, cap: int = HUMAN_CAP) -> list[str]:
    """Compact text for an agent: capped lists, best center per shape."""
    lines = [f"board {result['width']}x{result['height']}  closed={result['closed']}  flagged={result['flagged']}"]
    if result.get("generated") is False:
        lines.append(
            "BOARD NOT GENERATED: mines are placed after the first open, any cell is safe. "
            f"Open the centre: open {result['width'] // 2} {result['height'] // 2}"
        )
        return lines

    def capped(items: list, render) -> None:
        for item in items[:cap]:
            lines.append("  " + render(item))
        if len(items) > cap:
            lines.append(f"  (+{len(items) - cap} more)")

    if result["safe_opens"]:
        lines.append("SAFE OPENS")
        capped(result["safe_opens"], lambda item: f"{item['x']},{item['y']}  {item['reason']}")
    else:
        lines.append("SAFE OPENS  (none proven)")
    if result["chords"]:
        lines.append("CHORDS")
        capped(
            result["chords"],
            lambda item: f"chord {item['x']},{item['y']} -> " + " ".join(f"{c['x']},{c['y']}" for c in item["opens"]),
        )
    if result["flags"]:
        lines.append("FLAGS")
        capped(result["flags"], lambda item: f"{item['x']},{item['y']}  {item['reason']}")
    if not result["safe_opens"] and result["guesses"]:
        lines.append("GUESSES (lowest local mine chance, not proven)")
        capped(result["guesses"][:6], lambda item: f"{item['x']},{item['y']}  p_mine={item['p_mine']:.2f}  {item['reason']}")
    if result["scout_targets"]:
        lines.append("SCOUT TARGETS (own board, best center per shape; one line per center)")
        for line in _scout_lines(_first_per_shape(result["scout_targets"], "shape", "size")):
            lines.append("  " + line)
    if result.get("zipzap_targets"):
        lines.append("ZIPZAP TARGETS (own board)")
        for item in result["zipzap_targets"][:3]:
            lines.append(f"  {item['x']},{item['y']}  {item['reason']}")
    if result["attack_targets"]:
        lines.append("ATTACK TARGETS (opponent board, best center per shape and metric)")
        for item in _first_per_shape(result["attack_targets"], "shape", "size", "metric"):
            lines.append(f"  {item['x']},{item['y']}  {item['reason']}")
    return lines


def _scout_lines(items: list[dict]) -> list[str]:
    """Group the per-shape best centers by cell: `1,1  rhombus 3: 5 unknown, cross 3: 5 unknown`."""
    grouped: dict[tuple[int, int], list[str]] = {}
    for item in items:
        grouped.setdefault((item["x"], item["y"]), []).append(f"{item['shape'].lower()} {item['size']}: {item['unknown']} unknown")
    return [f"{x},{y}  " + ", ".join(shapes) for (x, y), shapes in grouped.items()]


def _first_per_shape(items: list[dict], *keys: str) -> list[dict]:
    seen: set[tuple] = set()
    result = []
    for item in items:
        key = tuple(item[k] for k in keys)
        if key in seen:
            continue
        seen.add(key)
        result.append(item)
    return result


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

    # Rhombus 4 shape must match Shared.RhombusShape: 4x4 grid without corners = 12 cells.
    rhombus = _pattern_cells(_rhombus_grid(4), (5, 5))
    if len(rhombus) != 12 or (3, 3) in rhombus or (6, 6) in rhombus or (3, 4) not in rhombus or (6, 5) not in rhombus:
        print("self-test rhombus shape failed", sorted(rhombus), file=sys.stderr)
        return False

    # 8x8 board with the top-left 4x4 opened: best scout center sits in the far quadrant.
    rows = []
    for y in range(8):
        rows.append("".join("0" if x < 4 and y < 4 else "." for x in range(8)))
    result = solve({"Self": {"BoardAscii": "\n".join(rows)}})
    top = result["scout_targets"][0]
    if top["x"] < 4 or top["y"] < 4 or top["unknown"] != 12:
        print("self-test scout target failed", top, file=sys.stderr)
        return False

    # Opponent 1x2 board: clue 1 proves the only closed cell is a mine, so no closed-metric
    # target survives, while the open-metric target still points at the open cell.
    result = solve({"Self": {"BoardAscii": "..\n.."}, "Opponent": {"BoardAscii": "1."}})
    closed_targets = [item for item in result["attack_targets"] if item["metric"] == "closed"]
    open_targets = [item for item in result["attack_targets"] if item["metric"] == "open"]
    if closed_targets or not open_targets:
        print("self-test attack target failed", result["attack_targets"], file=sys.stderr)
        return False

    # No opponent board yet: no attack targets, no crash.
    result = solve({"Self": {"BoardAscii": "..\n.."}, "Opponent": {"BoardAscii": ""}})
    if result["attack_targets"]:
        print("self-test empty opponent failed", file=sys.stderr)
        return False

    print("self-test ok")
    return True


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except BrokenPipeError:
        raise SystemExit(0)
