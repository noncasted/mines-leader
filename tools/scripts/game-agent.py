#!/usr/bin/env python3
"""CLI for the vs-bot agent play tools.

Unity MCP lists game_open(x, y) in the Editor window, but Coplay HTTP does not
publish those parameterized tools to tools/list. This script calls them the way
the server accepts: execute_custom_tool(tool_name, parameters).

  python3 tools/scripts/game-agent.py status
  python3 tools/scripts/game-agent.py start
  python3 tools/scripts/game-agent.py state
  python3 tools/scripts/game-agent.py wait
  python3 tools/scripts/game-agent.py open 3 4
  python3 tools/scripts/game-agent.py chord 3 4
  python3 tools/scripts/game-agent.py flag 15 15
  python3 tools/scripts/game-agent.py unflag 15 15
  python3 tools/scripts/game-agent.py use-card <card-guid> [--x N] [--y N]
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


def _xy(args: argparse.Namespace) -> dict:
    return {"x": args.x, "y": args.y}


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Call Unity game_* MCP tools via execute_custom_tool.",
    )
    parser.add_argument("--url", default=DEFAULT_URL, help="MCP HTTP endpoint")
    sub = parser.add_subparsers(dest="cmd", required=True)

    sub.add_parser("status", help="Match active / whose turn")
    sub.add_parser("start", help="Start LastManStandingTurnBased vs bot")
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
    use_card = sub.add_parser("use-card", help="Play a hand card")
    use_card.add_argument("card_id")
    use_card.add_argument("--x", type=int)
    use_card.add_argument("--y", type=int)
    use_card.add_argument("--extra-card-id")
    use_card.add_argument("--chosen-index", type=int)
    end_turn = sub.add_parser("end-turn", help="Skip and wait for next own turn")
    end_turn.add_argument("--timeout-ms", type=int, default=60000)

    args = parser.parse_args()

    try:
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
        return client.game("game_start_vs_bot")
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
        params: dict = {"card_id": args.card_id}
        if args.x is not None:
            params["x"] = args.x
        if args.y is not None:
            params["y"] = args.y
        if args.extra_card_id:
            params["extra_card_id"] = args.extra_card_id
        if args.chosen_index is not None:
            params["chosen_index"] = args.chosen_index
        return client.game("game_use_card", params)
    if command == "end-turn":
        return client.game(
            "game_end_turn",
            {"timeout_ms": args.timeout_ms},
            timeout=max(args.timeout_ms // 1000 + 15, 30),
        )
    raise McpError(f"unknown command {command}")


if __name__ == "__main__":
    sys.exit(main())
