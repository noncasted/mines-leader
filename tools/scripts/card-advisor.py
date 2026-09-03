#!/usr/bin/env python3
"""Ask OpenCode Go which card to play. Does not think about cell opens.

Default model: gpt-5.6-luna (alias: gpt-luna).

  python3 tools/scripts/card-advisor.py
  python3 tools/scripts/card-advisor.py --from-agent
  python3 tools/scripts/card-advisor.py obs.json
  python3 tools/scripts/card-advisor.py --apply

Credentials, first match wins:
  OPENCODE_API_KEY / OPENCODE_GO_API_KEY
  ~/.local/share/opencode/auth.json  (providers opencode-go, opencode)
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import urllib.error
import urllib.request
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_URL = "https://opencode.ai/zen/go/v1/chat/completions"
DEFAULT_MODEL = "gpt-5.6-luna"
MODEL_ALIASES = {
    "gpt-luna": "gpt-5.6-luna",
    "luna": "gpt-5.6-luna",
}
AUTH_PATH = Path.home() / ".local/share/opencode/auth.json"
USER_AGENT = "mines-leader-card-advisor/1.0"

# Fallback only. Since agent_card_play the observation carries Target / Shape / Size /
# NeedsPosition / Summary per hand card (shared/Game/Agent/AgentCardCatalog.cs); those
# fields win when present. This table covers observations from older builds.
# target: own_board | opponent_board | self | opponent
# needs_cell is true only for board targets
CARDS: dict[str, dict] = {
    "Bloodhound": {"mana": 2, "target": "own_board", "summary": "Diamond 4 on your board: unflag and open cells."},
    "ErosionDozer": {"mana": 4, "target": "own_board", "summary": "Clear up to 5 connected closed cells from click."},
    "ZipZap": {"mana": 3, "target": "own_board", "summary": "Chain-defuse up to 3 nearby mines from click, radius 4."},
    "MinefieldScout": {"mana": 3, "target": "own_board", "summary": "Line 5: flag mines, open safe cells."},
    "Sonar": {"mana": 3, "target": "own_board", "summary": "Diamond 4: flag mines, do not open."},
    "ThermalVision": {"mana": 3, "target": "own_board", "summary": "Diamond 3: highlight mines this turn only."},
    "ChaosDiamond": {"mana": 2, "target": "own_board", "summary": "Random diamond 2-5: flag mines, open safe."},
    "ChaosScout": {"mana": 2, "target": "own_board", "summary": "Random line 3-7: flag mines, open safe."},
    "Excavator": {"mana": 3, "target": "own_board", "summary": "Cross 2: flag mines, open safe."},
    "FortuneCookie": {"mana": 1, "target": "self", "summary": "Reveal 1-3 random own mines as a highlight."},
    "Gravedigger": {"mana": 4, "target": "self", "summary": "Return last discarded card."},
    "Scavenger": {"mana": 2, "target": "self", "summary": "Draw 2."},
    "HandScramble": {"mana": 3, "target": "opponent", "summary": "Shuffle opponent hand."},
    "MirrorMatch": {"mana": 3, "target": "self", "summary": "Copy opponent last card."},
    "MysticDraw": {"mana": 1, "target": "self", "summary": "Coin: +2 or -2 cards."},
    "Recycler": {"mana": 1, "target": "self", "summary": "Discard 1, draw 2."},
    "SabotageDeck": {"mana": 2, "target": "opponent", "summary": "Insert Dud into opponent deck."},
    "Salvage": {"mana": 2, "target": "self", "summary": "Look at top 3, take 1."},
    "CardThief": {"mana": 3, "target": "opponent", "summary": "Steal a random opponent hand card."},
    "Dud": {"mana": 99, "target": "self", "summary": "Dummy, always fails."},
    "TrebuchetAimer": {"mana": 2, "target": "self", "summary": "Next Trebuchet +2 cells. Stacks."},
    "Overclock": {"mana": 3, "target": "self", "summary": "+2 moves."},
    "Medic": {"mana": 4, "target": "self", "summary": "+1 HP."},
    "Purge": {"mana": 2, "target": "self", "summary": "Strip enemy effects from you."},
    "Adrenaline": {"mana": 1, "target": "self", "summary": "+1 move."},
    "ManaSurge": {"mana": 2, "target": "self", "summary": "+3 temp mana."},
    "BloodPact": {"mana": 0, "target": "self", "summary": "-1 HP, +3 mana, +2 moves."},
    "CoinToss": {"mana": 1, "target": "self", "summary": "Coin: +2 moves or -1 move."},
    "ManaFountain": {"mana": 1, "target": "self", "summary": "Random 1-5 mana."},
    "Focus": {"mana": 1, "target": "self", "summary": "Next card -1 mana."},
    "Shield": {"mana": 2, "target": "self", "summary": "Absorb next mine hit."},
    "PowerSurge": {"mana": 3, "target": "self", "summary": "All cards -1 mana this turn."},
    "Trebuchet": {"mana": 3, "target": "opponent_board", "summary": "Plant mines in diamond 4 on enemy board."},
    "ChainReaction": {"mana": 4, "target": "opponent_board", "summary": "Chain mines around found mines from click."},
    "OpponentBomb": {"mana": 2, "target": "opponent_board", "summary": "Open one enemy cell; mine deals 1."},
    "OpponentFlagErase": {"mana": 3, "target": "opponent_board", "summary": "Remove enemy flags in diamond 3."},
    "OpponentFlagReshuffle": {"mana": 2, "target": "opponent_board", "summary": "Shuffle enemy flags in diamond 3."},
    "Smoke": {"mana": 3, "target": "opponent_board", "summary": "Fog diamond 3 for 3 turns."},
    "FogOfWar": {"mana": 3, "target": "opponent_board", "summary": "Hide numbers diamond 4 for 2 turns."},
    "MineCluster": {"mana": 3, "target": "opponent_board", "summary": "Plant mines in a cross 2."},
    "CarpetBomb": {"mana": 5, "target": "opponent_board", "summary": "Plant mines in a line 5."},
    "FortuneBlast": {"mana": 2, "target": "opponent_board", "summary": "Random diamond 1-4 of mines."},
    "ChaosFog": {"mana": 2, "target": "opponent_board", "summary": "Random smoke diamond 1-4 for 3 turns."},
    "Frost": {"mana": 2, "target": "opponent_board", "summary": "Freeze diamond 2 for 1 turn."},
    "Blackout": {"mana": 2, "target": "opponent_board", "summary": "Hide numbers diamond 2 for 2 turns."},
    "DimensionRift": {"mana": 4, "target": "opponent_board", "summary": "Swap diamond 2 areas between boards."},
    "Siphon": {"mana": 2, "target": "opponent", "summary": "Drain 1 max mana from opponent."},
    "Lockdown": {"mana": 3, "target": "opponent", "summary": "-1 opponent move for 2 turns."},
    "DoubleOrNothing": {"mana": 2, "target": "self", "summary": "Coin: double mana or zero it."},
    "Embargo": {"mana": 3, "target": "opponent", "summary": "Opponent cards +1 mana."},
    "GamblersRuin": {"mana": 2, "target": "self", "summary": "Coin: +3 cards/+2 mana or -2 cards."},
    "SoulLink": {"mana": 3, "target": "opponent", "summary": "Reflect mine damage for 2 turns."},
}

SYSTEM_PROMPT = """You are a fast card advisor for a competitive minesweeper duel (Last Man Standing, turn-based).
You only decide whether to play a card this turn, which card, and where.
You do not open cells. Skip if no card is worth the mana.

Board ASCII:
  . closed   F flag   0-8 opened number   * exploded   ~ fogged closed
Coordinates are 0-based, x right, y down. First character of a row is x=0.

Reply with a single JSON object, no markdown:
{"action":"use_card"|"skip","card":"TypeName or null","card_id":"guid or null","x":null or int,"y":null or int,"reason":"one short sentence"}
Use a card_id from the hand list. If the card targets a board, x and y are required.
Prefer spending leftover mana on scout/pressure over doing nothing.
If health is 1, value Medic/Shield. If the enemy board has a dense closed pocket, Trebuchet/MineCluster there.
"""


def main() -> int:
    parser = argparse.ArgumentParser(description="Ask OpenCode Go which card to play.")
    parser.add_argument("source", nargs="?", help="Observation JSON, else MCP state")
    parser.add_argument("--from-agent", action="store_true")
    parser.add_argument("--apply", action="store_true", help="Send game_use_card after a use_card answer")
    parser.add_argument("--model", default=os.environ.get("OPENCODE_GO_MODEL", DEFAULT_MODEL))
    parser.add_argument("--url", default=os.environ.get("OPENCODE_GO_URL", DEFAULT_URL))
    parser.add_argument("--dry-prompt", action="store_true", help="Print the prompt and exit")
    args = parser.parse_args()

    observation = _load_observation(args.source, args.from_agent)
    prompt = build_prompt(observation)
    if args.dry_prompt:
        print(prompt)
        return 0

    raw = ask_model(prompt, model=resolve_model(args.model), url=args.url)
    advice = parse_advice(raw, observation)
    json.dump(advice, sys.stdout, indent=2, ensure_ascii=False)
    sys.stdout.write("\n")

    if args.apply and advice.get("action") == "use_card" and advice.get("card_id"):
        applied = _apply(advice)
        print("--- applied ---", file=sys.stderr)
        json.dump(applied, sys.stdout, indent=2, ensure_ascii=False)
        sys.stdout.write("\n")
    return 0


def build_prompt(observation: dict) -> str:
    selfp = observation.get("Self") or observation.get("self") or {}
    opponent = observation.get("Opponent") or observation.get("opponent") or {}
    hand = selfp.get("Hand") or selfp.get("hand") or []
    mana = selfp.get("Mana", selfp.get("mana"))
    mana_max = selfp.get("ManaMax", selfp.get("manaMax"))
    health = selfp.get("Health", selfp.get("health"))
    health_max = selfp.get("HealthMax", selfp.get("healthMax"))
    moves = selfp.get("MovesLeft", selfp.get("movesLeft"))
    events = observation.get("Events") or observation.get("events") or []

    lines = [
        f"Your turn: {observation.get('IsOwnTurn', observation.get('isOwnTurn'))}",
        f"You: HP {health}/{health_max}  mana {mana}/{mana_max}  moves {moves}/{selfp.get('MovesMax', selfp.get('movesMax'))}  flags {selfp.get('Flags', selfp.get('flags'))}  mines {selfp.get('Mines', selfp.get('mines'))}",
        f"Enemy: HP {opponent.get('Health', opponent.get('health'))}/{opponent.get('HealthMax', opponent.get('healthMax'))}  mana {opponent.get('Mana', opponent.get('mana'))}/{opponent.get('ManaMax', opponent.get('manaMax'))}  flags {opponent.get('Flags', opponent.get('flags'))}",
        f"Modifiers you: {selfp.get('Modifiers') or selfp.get('modifiers') or []}",
        f"Modifiers enemy: {opponent.get('Modifiers') or opponent.get('modifiers') or []}",
        "",
        "YOUR BOARD",
        _ascii(selfp),
        "",
        "ENEMY BOARD",
        _ascii(opponent),
        "",
        "HAND (only play a card listed here, and only if mana >= cost)",
    ]
    affordable = []
    for card in hand:
        name = card.get("Type") or card.get("type") or "?"
        card_id = card.get("Id") or card.get("id")
        cost = int(card.get("ManaCost") or card.get("manaCost") or CARDS.get(name, {}).get("mana") or 0)
        info = _card_info(card, name)
        needs = info["needs_cell"]
        shape = f" shape={info['shape']} size={info['size']}" if info.get("shape") else ""
        mark = "AFFORDABLE" if isinstance(mana, int) and mana >= cost else "TOO EXPENSIVE"
        lines.append(
            f"- {name} id={card_id} cost={cost} target={info['target']} needs_cell={needs}{shape} [{mark}] {info['summary']}"
        )
        if mark == "AFFORDABLE":
            affordable.append(name)
    if not affordable:
        lines.append("No affordable cards. Answer skip.")
    if events:
        lines.append("")
        lines.append("RECENT EVENTS")
        for event in events[-25:]:
            lines.append(f"- {event}")
    lines.append("")
    lines.append("Pick one JSON action now.")
    return "\n".join(lines)


_TARGET_NAMES = {
    "OwnBoard": "own_board",
    "OpponentBoard": "opponent_board",
    "Self": "self",
    "Opponent": "opponent",
}


def _card_info(card: dict, name: str) -> dict:
    """Observation fields first (AgentCardView), CARDS table as fallback."""
    summary = card.get("Summary") or card.get("summary")
    if summary:
        target = _TARGET_NAMES.get(str(card.get("Target") or card.get("target")), "self")
        return {
            "target": target,
            "needs_cell": bool(card.get("NeedsPosition", card.get("needsPosition", False))),
            "shape": card.get("Shape") or card.get("shape") or "",
            "size": card.get("Size", card.get("size", 0)),
            "summary": summary,
        }

    fallback = CARDS.get(name, {"target": "self", "summary": "Unknown card. If unsure, skip."})
    return {
        "target": fallback.get("target", "self"),
        "needs_cell": fallback.get("target") in {"own_board", "opponent_board"},
        "shape": "",
        "size": 0,
        "summary": fallback.get("summary", ""),
    }


def resolve_model(name: str) -> str:
    return MODEL_ALIASES.get(name.strip().lower(), name)


def ask_model(prompt: str, model: str, url: str) -> str:
    key = load_api_key()
    body = {
        "model": model,
        "temperature": 0.2,
        "messages": [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": prompt},
        ],
    }
    request = urllib.request.Request(
        url,
        data=json.dumps(body).encode(),
        method="POST",
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json",
            "Authorization": f"Bearer {key}",
            "User-Agent": USER_AGENT,
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=90) as response:
            payload = json.loads(response.read().decode())
    except urllib.error.HTTPError as error:
        detail = error.read().decode(errors="replace")[:1500]
        raise SystemExit(f"{model} HTTP {error.code}: {detail}") from error
    except urllib.error.URLError as error:
        raise SystemExit(f"{model} unreachable at {url}: {error.reason}") from error

    try:
        return payload["choices"][0]["message"]["content"]
    except (KeyError, IndexError, TypeError) as error:
        raise SystemExit(f"unexpected {model} payload: {json.dumps(payload)[:1500]}") from error


def parse_advice(raw: str, observation: dict) -> dict:
    data = _extract_json(raw)
    hand = (observation.get("Self") or observation.get("self") or {}).get("Hand") or []
    by_id = {str(card.get("Id") or card.get("id")): card for card in hand}
    by_type: dict[str, list[dict]] = {}
    for card in hand:
        by_type.setdefault(str(card.get("Type") or card.get("type")), []).append(card)

    action = data.get("action") or "skip"
    name = data.get("card")
    card_id = data.get("card_id") or data.get("cardId")
    if action != "use_card":
        return {
            "action": "skip",
            "card": None,
            "card_id": None,
            "x": None,
            "y": None,
            "reason": data.get("reason") or "skip",
            "raw": raw,
        }

    card = by_id.get(str(card_id)) if card_id else None
    if card is None and name in by_type:
        card = by_type[name][0]
        card_id = card.get("Id") or card.get("id")
        name = card.get("Type") or card.get("type")
    if card is None:
        return {
            "action": "skip",
            "card": None,
            "card_id": None,
            "x": None,
            "y": None,
            "reason": f"model named unknown card {name!r} {card_id!r}",
            "raw": raw,
        }

    name = card.get("Type") or card.get("type") or name
    info = _card_info(card, str(name))
    x = data.get("x")
    y = data.get("y")
    if info["needs_cell"] and (x is None or y is None):
        return {
            "action": "skip",
            "card": name,
            "card_id": str(card.get("Id") or card.get("id")),
            "x": x,
            "y": y,
            "reason": f"{name} needs a cell and the model omitted x/y",
            "raw": raw,
        }

    return {
        "action": "use_card",
        "card": name,
        "card_id": str(card.get("Id") or card.get("id")),
        "x": x,
        "y": y,
        "reason": data.get("reason") or "",
        "raw": raw,
    }


def load_api_key() -> str:
    for name in ("OPENCODE_API_KEY", "OPENCODE_GO_API_KEY"):
        value = os.environ.get(name)
        if value:
            return value.strip()

    if AUTH_PATH.exists():
        data = json.loads(AUTH_PATH.read_text())
        for provider in ("opencode-go", "opencode", "opencode-zen", "zen"):
            entry = data.get(provider)
            key = _entry_key(entry)
            if key:
                return key
        if isinstance(data, dict):
            for entry in data.values():
                key = _entry_key(entry)
                if key:
                    return key

    raise SystemExit(
        "No OpenCode Go key. Set OPENCODE_API_KEY or run `opencode auth` "
        f"and store the Go key in {AUTH_PATH} under provider opencode-go."
    )


def _entry_key(entry: object) -> str | None:
    if isinstance(entry, str) and entry.strip():
        return entry.strip()
    if not isinstance(entry, dict):
        return None
    for field in ("key", "apiKey", "api_key", "token", "access"):
        value = entry.get(field)
        if isinstance(value, str) and value.strip():
            return value.strip()
    return None


def _load_observation(source: str | None, from_agent: bool) -> dict:
    if from_agent or source is None:
        if source and source != "--from-agent" and Path(source).exists():
            return json.loads(Path(source).read_text())
        if source is None and not sys.stdin.isatty():
            raw = sys.stdin.read()
            if raw.strip():
                return json.loads(raw)
        return _from_agent()
    return json.loads(Path(source).read_text())


def _from_agent() -> dict:
    completed = subprocess.run(
        [sys.executable, str(SCRIPT_DIR / "game-agent.py"), "--json", "state"],
        check=False,
        capture_output=True,
        text=True,
    )
    if completed.returncode != 0:
        raise SystemExit(completed.stderr.strip() or "game-agent.py state failed")
    return json.loads(completed.stdout)


def _apply(advice: dict) -> object:
    command = [
        sys.executable,
        str(SCRIPT_DIR / "game-agent.py"),
        "use-card",
        advice["card_id"],
    ]
    if advice.get("x") is not None:
        command.extend(["--x", str(advice["x"])])
    if advice.get("y") is not None:
        command.extend(["--y", str(advice["y"])])
    completed = subprocess.run(command, check=False, capture_output=True, text=True)
    if completed.returncode != 0:
        raise SystemExit(completed.stderr.strip() or completed.stdout.strip() or "use-card failed")
    return json.loads(completed.stdout)


def _ascii(player: dict) -> str:
    text = player.get("BoardAscii") or player.get("boardAscii")
    if text:
        numbered = ["    " + "".join(f"{x % 10}" for x in range(len(text.splitlines()[0])))]
        for y, row in enumerate(text.splitlines()):
            numbered.append(f"{y:02d}  {row}")
        return "\n".join(numbered)
    return "(empty board)"


def _extract_json(raw: str) -> dict:
    text = raw.strip()
    fenced = re.search(r"```(?:json)?\s*(\{.*?\})\s*```", text, re.S)
    if fenced:
        text = fenced.group(1)
    else:
        start = text.find("{")
        end = text.rfind("}")
        if start >= 0 and end > start:
            text = text[start : end + 1]
    try:
        data = json.loads(text)
    except json.JSONDecodeError as error:
        raise SystemExit(f"model did not return JSON:\n{raw}") from error
    if not isinstance(data, dict):
        raise SystemExit(f"model JSON is not an object:\n{raw}")
    return data


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except BrokenPipeError:
        raise SystemExit(0)
