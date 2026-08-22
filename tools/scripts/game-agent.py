#!/usr/bin/env python3
"""CLI for the vs-bot agent play tools.

Unity MCP lists game_open(x, y) in the Editor window, but Coplay HTTP does not
publish those parameterized tools to tools/list. This script calls them the way
the server accepts: execute_custom_tool(tool_name, parameters).

  python3 tools/scripts/game-agent.py status
  python3 tools/scripts/game-agent.py scenarios
  python3 tools/scripts/game-agent.py start --scenario scout_vs_easy
  python3 tools/scripts/game-agent.py start --bot Hard --deck Bloodhound Medic --bot-deck Trebuchet
  python3 tools/scripts/game-agent.py state
  python3 tools/scripts/game-agent.py wait
  python3 tools/scripts/game-agent.py open 3 4
  python3 tools/scripts/game-agent.py chord 3 4
  python3 tools/scripts/game-agent.py flag 15 15
  python3 tools/scripts/game-agent.py unflag 15 15
  python3 tools/scripts/game-agent.py use-card [<card-guid>] [--type Bloodhound] [--x N] [--y N]
  python3 tools/scripts/game-agent.py legal-plays
  python3 tools/scripts/game-agent.py inspect 3 4
  python3 tools/scripts/game-agent.py wait-visual
  python3 tools/scripts/game-agent.py end-turn

Needs Unity play mode (GameMock LastManStandingTurnBased) and a running cluster.
MCP URL: $GAME_AGENT_MCP_URL or http://127.0.0.1:8080/mcp

Sibling helpers (they do not send moves):
  python3 tools/scripts/board-solver.py --from-agent
  python3 tools/scripts/card-advisor.py --from-agent
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path

SCENARIOS_DIR = Path(__file__).resolve().parent / "agent-scenarios"

DEFAULT_URL = os.environ.get("GAME_AGENT_MCP_URL", "http://127.0.0.1:8080/mcp")


class McpError(RuntimeError):
    pass


class McpClient:
    def __init__(self, url: str) -> None:
        self.url = url
        self._id = 0
        self.session_id = self._initialize()

    def _initialize(self) -> str:
        body = {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "game-agent", "version": "1"},
            },
        }
        headers, raw = self._post(body, session_id=None)
        session_id = headers.get("Mcp-Session-Id") or headers.get("mcp-session-id")
        if not session_id:
            raise McpError("MCP initialize did not return mcp-session-id")
        self._post(
            {"jsonrpc": "2.0", "method": "notifications/initialized"},
            session_id=session_id,
        )
        return session_id

    def call(self, name: str, arguments: dict | None = None, timeout: int = 120) -> object:
        self._id += 1
        result = self._rpc(
            "tools/call",
            {"name": name, "arguments": arguments or {}},
            timeout=timeout,
        )
        return _unwrap_tool_result(result)

    def game(self, tool_name: str, parameters: dict | None = None, timeout: int = 120) -> object:
        try:
            direct = self.call(tool_name, parameters, timeout=timeout)
            if _looks_like_unknown_or_schema_error(direct):
                return self.call(
                    "execute_custom_tool",
                    {"tool_name": tool_name, "parameters": parameters or {}},
                    timeout=timeout,
                )
            return direct
        except McpError as error:
            if "Unknown tool" in str(error) or "validation" in str(error).lower():
                return self.call(
                    "execute_custom_tool",
                    {"tool_name": tool_name, "parameters": parameters or {}},
                    timeout=timeout,
                )
            raise

    def _rpc(self, method: str, params: dict, timeout: int = 60) -> object:
        self._id += 1
        req_id = self._id
        _, raw = self._post(
            {"jsonrpc": "2.0", "id": req_id, "method": method, "params": params},
            session_id=self.session_id,
            timeout=timeout,
        )
        for line in raw.splitlines():
            if not line.startswith("data: "):
                continue
            message = json.loads(line[6:])
            if message.get("id") != req_id:
                continue
            if "error" in message:
                raise McpError(json.dumps(message["error"]))
            return message.get("result")
        raise McpError(raw[:2000] if raw else "empty MCP response")

    def _post(
        self,
        payload: dict,
        session_id: str | None,
        timeout: int = 60,
    ) -> tuple[dict[str, str], str]:
        headers = {
            "Content-Type": "application/json",
            "Accept": "application/json, text/event-stream",
        }
        if session_id:
            headers["Mcp-Session-Id"] = session_id
        request = urllib.request.Request(
            self.url,
            data=json.dumps(payload).encode(),
            method="POST",
            headers=headers,
        )
        try:
            with urllib.request.urlopen(request, timeout=timeout) as response:
                header_map = {key: value for key, value in response.headers.items()}
                return header_map, response.read().decode()
        except urllib.error.HTTPError as error:
            detail = error.read().decode(errors="replace")[:500]
            raise McpError(f"HTTP {error.code} {error.reason}: {detail}") from error
        except urllib.error.URLError as error:
            raise McpError(f"Cannot reach MCP at {self.url}: {error.reason}") from error


def _unwrap_tool_result(result: object) -> object:
    if not isinstance(result, dict):
        return result
    if "structuredContent" in result:
        return _unwrap_payload(result["structuredContent"])
    texts: list[object] = []
    for item in result.get("content") or []:
        if not isinstance(item, dict) or item.get("type") != "text":
            continue
        text = item.get("text")
        try:
            texts.append(json.loads(text))
        except (TypeError, json.JSONDecodeError):
            texts.append(text)
    if len(texts) == 1:
        return _unwrap_payload(texts[0])
    if texts:
        return texts
    return _unwrap_payload(result)


def _unwrap_payload(payload: object) -> object:
    if isinstance(payload, dict) and "data" in payload and isinstance(payload["data"], dict):
        inner = payload["data"]
        if any(key in inner for key in ("HasError", "IsOwnTurn", "Self", "active", "sequence")):
            return inner
    return payload


def _looks_like_unknown_or_schema_error(payload: object) -> bool:
    text = payload if isinstance(payload, str) else json.dumps(payload)
    lowered = text.lower()
    return (
        "unknown tool" in lowered
        or "unexpected keyword" in lowered
        or "validation error" in lowered
    )


def _observation_failed(payload: object) -> bool:
    if not isinstance(payload, dict):
        return False
    return payload.get("HasError") is True or payload.get("hasError") is True


def _print(payload: object) -> None:
    json.dump(payload, sys.stdout, indent=2, ensure_ascii=False)
    sys.stdout.write("\n")


def _list_scenarios() -> object:
    if SCENARIOS_DIR.is_dir() is False:
        return {"scenarios": [], "dir": str(SCENARIOS_DIR)}

    names = sorted(path.stem for path in SCENARIOS_DIR.glob("*.json"))
    return {"dir": str(SCENARIOS_DIR), "scenarios": names}


def _load_scenario(name: str) -> dict:
    path = Path(name)
    if path.suffix.lower() != ".json":
        path = SCENARIOS_DIR / f"{name}.json"
    elif path.is_file() is False:
        path = SCENARIOS_DIR / path.name

    if path.is_file() is False:
        raise McpError(f"Scenario not found: {name} (looked in {path})")

    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise McpError(f"Invalid scenario JSON {path}: {error}") from error

    if isinstance(data, dict) is False:
        raise McpError(f"Scenario {path} must be a JSON object")

    return data


def _start_params(args: argparse.Namespace) -> dict:
    params: dict = {}
    if args.scenario:
        params.update(_load_scenario(args.scenario))

    if args.board:
        params["board"] = args.board
    if args.hand:
        params["hand"] = args.hand
    if args.deck:
        params["deck"] = args.deck
    if args.bot_deck:
        params["bot_deck"] = args.bot_deck
    if args.bot:
        params["bot"] = args.bot
    if args.human_first is not None:
        params["human_goes_first"] = args.human_first
    if args.mana is not None:
        params["mana"] = args.mana
    if args.moves is not None:
        params["moves"] = args.moves
    return params


def _xy(args: argparse.Namespace) -> dict:
    return {"x": args.x, "y": args.y}


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Call Unity game_* MCP tools via execute_custom_tool.",
    )
    parser.add_argument("--url", default=DEFAULT_URL, help="MCP HTTP endpoint")
    sub = parser.add_subparsers(dest="cmd", required=True)

    sub.add_parser("status", help="Match active / whose turn")
    start = sub.add_parser("start", help="Start LastManStandingTurnBased vs bot")
    start.add_argument("--scenario", help="Named JSON in tools/scripts/agent-scenarios/, or a .json path")
    start.add_argument("--board", help="Self board layout DSL (BoardParser alphabet)")
    start.add_argument("--hand", nargs="+", help="CardType names for the human opening hand")
    start.add_argument("--deck", nargs="+", help="CardType names for the human draw pile")
    start.add_argument("--bot-deck", nargs="+", dest="bot_deck", help="CardType names for the bot draw pile")
    start.add_argument("--bot", choices=["Easy", "Medium", "Hard"], help="Bot difficulty for this match")
    start.add_argument("--human-first", dest="human_first", action=argparse.BooleanOptionalAction, default=None)
    start.add_argument("--mana", type=int)
    start.add_argument("--moves", type=int)
    sub.add_parser("scenarios", help="List named start scenarios")
    state = sub.add_parser("state", help="Last observation")
    state.add_argument("--oracle", action="store_true")
    wait = sub.add_parser("wait", help="Wait until own turn or game over")
    wait.add_argument("--timeout-ms", type=int, default=60000)
    for name, help_text in (
        ("open", "Open a cell"),
        ("chord", "Chord-open a cell"),
        ("flag", "Flag a cell"),
        ("unflag", "Remove a flag"),
    ):
        item = sub.add_parser(name, help=help_text)
        item.add_argument("x", type=int)
        item.add_argument("y", type=int)
    use_card = sub.add_parser("use-card", help="Play a hand card by id or type")
    use_card.add_argument("card_id", nargs="?", help="Hand card guid")
    use_card.add_argument("--type", help="CardType name, e.g. Bloodhound")
    use_card.add_argument("--x", type=int)
    use_card.add_argument("--y", type=int)
    use_card.add_argument("--extra-card-id")
    use_card.add_argument("--chosen-index", type=int)
    sub.add_parser("legal-plays", help="Legal cards and cells from server rules")
    inspect = sub.add_parser("inspect", help="Unity cell inspect (not server ASCII)")
    inspect.add_argument("x", type=int)
    inspect.add_argument("y", type=int)
    inspect.add_argument("--opponent", action="store_true")
    wait_visual = sub.add_parser("wait-visual", help="Wait until own-board cell animations finish")
    wait_visual.add_argument("--timeout-ms", type=int, default=5000)
    end_turn = sub.add_parser("end-turn", help="Skip and wait for next own turn")
    end_turn.add_argument("--timeout-ms", type=int, default=60000)

    args = parser.parse_args()

    try:
        if args.cmd == "scenarios":
            payload = _list_scenarios()
        else:
            client = McpClient(args.url)
            payload = _dispatch(client, args)
    except McpError as error:
        print(error, file=sys.stderr)
        return 2

    _print(payload)
    return 1 if _observation_failed(payload) else 0


def _dispatch(client: McpClient, args: argparse.Namespace) -> object:
    command = args.cmd
    if command == "status":
        return client.game("game_status")
    if command == "start":
        params = _start_params(args)
        return client.game("game_start_vs_bot", params or None)
    if command == "state":
        params = {"oracle": True} if args.oracle else {}
        return client.game("game_get_state", params)
    if command == "wait":
        return client.game("game_wait_turn", {"timeout_ms": args.timeout_ms}, timeout=max(args.timeout_ms // 1000 + 15, 30))
    if command == "open":
        return client.game("game_open", _xy(args))
    if command == "chord":
        return client.game("game_chord", _xy(args))
    if command == "flag":
        return client.game("game_flag", _xy(args))
    if command == "unflag":
        return client.game("game_unflag", _xy(args))
    if command == "use-card":
        params: dict = {}
        if args.card_id:
            params["card_id"] = args.card_id
        if args.type:
            params["type"] = args.type
        if args.x is not None:
            params["x"] = args.x
        if args.y is not None:
            params["y"] = args.y
        if args.extra_card_id:
            params["extra_card_id"] = args.extra_card_id
        if args.chosen_index is not None:
            params["chosen_index"] = args.chosen_index
        return client.game("game_use_card", params)
    if command == "legal-plays":
        return client.game("game_legal_plays")
    if command == "inspect":
        params = _xy(args)
        if args.opponent:
            params["opponent"] = True
        return client.game("game_inspect_cell", params)
    if command == "wait-visual":
        return client.game(
            "game_wait_visual",
            {"timeout_ms": args.timeout_ms},
            timeout=max(args.timeout_ms // 1000 + 15, 30),
        )
    if command == "end-turn":
        return client.game(
            "game_end_turn",
            {"timeout_ms": args.timeout_ms},
            timeout=max(args.timeout_ms // 1000 + 15, 30),
        )
    raise McpError(f"unknown command {command}")


if __name__ == "__main__":
    sys.exit(main())
