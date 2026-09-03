#!/usr/bin/env python3
"""CLI for the vs-bot agent play tools.

Unity MCP lists game_open(x, y) in the Editor window, but Coplay HTTP does not
publish those parameterized tools to tools/list. This script calls them the way
the server accepts: execute_custom_tool(tool_name, parameters).

  python3 tools/scripts/game-agent.py preflight      # cluster 200 + MCP + no backend/shared edits after Silo start
  python3 tools/scripts/game-agent.py status
  python3 tools/scripts/game-agent.py ensure-play
  python3 tools/scripts/game-agent.py scenarios
  python3 tools/scripts/game-agent.py start --scenario scout_vs_easy
  python3 tools/scripts/game-agent.py start --bot Hard --deck Bloodhound Medic --bot-deck Trebuchet
  python3 tools/scripts/game-agent.py turn            # wait for turn_start + state + legal plays + solver
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
  python3 tools/scripts/game-agent.py diff
  python3 tools/scripts/game-agent.py stop

`start` and `ensure-play` set GameMock._mode to LastManStandingTurnBased (31)
and enter Play via manage_editor. Cluster must already be running.
With --scenario / --hand / --board / --bot / ... they write an AgentMatchFixture
JSON into EditorPrefs (GameMock.FixturePrefsKey) before Play; GameMock reads
and deletes the key once. A running match is never restarted silently: `stop`
first, then `start` again with the fixture.
MCP URL: $GAME_AGENT_MCP_URL or http://127.0.0.1:8080/mcp
Cluster health: $GAME_AGENT_CLUSTER_URL or http://localhost:7103/api/benchmarks

`preflight` (also run by `start`) checks the cluster answers 200, MCP answers,
and no file under backend/ or shared/ changed after the Silo process started;
a stale cluster plays with old rules, so `start` refuses (--ignore-stale to
override). It never starts the cluster or Unity.

Output is compact text by default: header, resources, hand, events tail, both
boards as ASCII; actions print one line with what changed (hp / mana / moves,
opened / flagged / closed cells, hand). `--json` before the command prints the
raw payload (full observation with Cells) for other scripts.

Every command that receives an observation (state / wait / turn / actions /
diff) saves it to $XDG_CACHE_HOME/game-agent/last-state.json (fallback
~/.cache/game-agent/) and reports changes against the previous one, so each
action line shows exactly what that action changed. `diff` compares a fresh
game_get_state with the saved one.

`wait` returns only on turn_start, game_over, or an own turn with moves left;
stale `action` frames with MovesLeft 0 (round closing while the bot plays) are
skipped. `end-turn` refuses when MovesLeft is 0: the round closes itself.

Sibling helpers (they do not send moves):
  python3 tools/scripts/board-solver.py --from-agent
  python3 tools/scripts/card-advisor.py --from-agent
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

SCENARIOS_DIR = Path(__file__).resolve().parent / "agent-scenarios"

DEFAULT_URL = os.environ.get("GAME_AGENT_MCP_URL", "http://127.0.0.1:8080/mcp")
CLUSTER_HEALTH_URL = os.environ.get("GAME_AGENT_CLUSTER_URL", "http://localhost:7103/api/benchmarks")
REPO_ROOT = Path(__file__).resolve().parents[2]
STALE_DIRS = ("backend", "shared")
GAME_FIELD_SCENE = "Assets/GamePlay/Scenes/Game_Field.unity"
TURN_BASED_MODE = 31
FIXTURE_PREFS_KEY = "MinesLeader.GameMock.Fixture"
CACHE_DIR = Path(os.environ.get("XDG_CACHE_HOME") or Path.home() / ".cache") / "game-agent"
LAST_STATE_PATH = CACHE_DIR / "last-state.json"

# CLI / scenario key -> AgentMatchFixture property (client/Assets/Common/Flow/Mocks/GameMock.cs).
FIXTURE_FIELDS = {
    "board": "SelfBoardLayout",
    "hand": "SelfHand",
    "deck": "SelfDeck",
    "bot_deck": "BotDeck",
    "bot": "BotProfile",
    "human_goes_first": "HumanGoesFirst",
    "mana": "Mana",
    "moves": "Moves",
}

_INSPECT_CODE = r'''
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var mock = UnityEngine.Object.FindFirstObjectByType<Flow.Mocks.GameMock>(UnityEngine.FindObjectsInactive.Include);
int mode = -1;
string mockName = "";
if (mock != null) {
    var so = new UnityEditor.SerializedObject(mock);
    var prop = so.FindProperty("_mode");
    mode = prop != null ? prop.intValue : -2;
    mockName = mock.gameObject.name;
}
return "{\"playing\":" + (UnityEditor.EditorApplication.isPlaying ? "true" : "false")
    + ",\"scene\":\"" + scene.name
    + "\",\"path\":\"" + scene.path
    + "\",\"hasMock\":" + (mock != null ? "true" : "false")
    + ",\"mock\":\"" + mockName
    + "\",\"mode\":" + mode
    + "}";
'''

_SET_MODE_CODE = r'''
var mock = UnityEngine.Object.FindFirstObjectByType<Flow.Mocks.GameMock>(UnityEngine.FindObjectsInactive.Include);
if (mock == null)
    return "{\"ok\":false,\"error\":\"GameMock not found\"}";
var so = new UnityEditor.SerializedObject(mock);
var prop = so.FindProperty("_mode");
if (prop == null)
    return "{\"ok\":false,\"error\":\"_mode property not found\"}";
prop.intValue = 31;
so.ApplyModifiedPropertiesWithoutUndo();
return "{\"ok\":true,\"mode\":" + prop.intValue + "}";
'''

_SET_FIXTURE_CODE = r'''
UnityEditor.EditorPrefs.SetString(%(key)s, %(json)s);
return "{\"ok\":true}";
'''


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


def _error(message: str) -> dict:
    return {"HasError": True, "Error": message}


def _connect(url: str, attempts: int = 20, delay: float = 1.0) -> McpClient:
    last_error: McpError | None = None
    for _ in range(attempts):
        try:
            return McpClient(url)
        except McpError as error:
            last_error = error
            time.sleep(delay)
    raise last_error or McpError(f"Cannot reach MCP at {url}")


def _code_result(payload: object) -> str:
    if isinstance(payload, dict) is False:
        raise McpError(f"unexpected execute_code payload: {payload}")
    if payload.get("success") is False or payload.get("isError") is True:
        raise McpError(str(payload.get("error") or payload.get("message") or payload))
    data = payload.get("data")
    if isinstance(data, dict) and "result" in data:
        return str(data["result"])
    if "result" in payload:
        return str(payload["result"])
    raise McpError(f"unexpected execute_code payload: {payload}")


def _inspect_editor(client: McpClient) -> dict:
    raw = _code_result(client.call("execute_code", {"action": "execute", "code": _INSPECT_CODE}))
    try:
        data = json.loads(raw)
    except json.JSONDecodeError as error:
        raise McpError(f"inspect JSON failed ({error}): {raw[:500]}") from error
    if isinstance(data, dict) is False:
        raise McpError(f"inspect did not return an object: {raw[:500]}")
    return data


def _editor_action(client: McpClient, action: str) -> None:
    try:
        client.call("manage_editor", {"action": action}, timeout=30)
    except McpError:
        # Entering/exiting play often drops the MCP socket during domain reload.
        return


def _wait_editor(url: str, playing: bool, attempts: int = 30) -> tuple[McpClient, dict]:
    last: dict = {}
    last_error: McpError | None = None
    for _ in range(attempts):
        try:
            client = _connect(url, attempts=3, delay=1.0)
            last = _inspect_editor(client)
            last_error = None
            if bool(last.get("playing")) == playing:
                return client, last
        except McpError as error:
            last_error = error
        time.sleep(1)
    detail = json.dumps(last) if last else str(last_error)
    raise McpError(f"Timed out waiting for isPlaying={playing}. Last: {detail}")


def _ensure_play(client: McpClient, fixture: dict | None = None) -> dict:
    url = client.url
    actions: list[str] = []

    try:
        status = client.game("game_status")
        if isinstance(status, dict) and status.get("active") is True:
            if fixture:
                return _error("Match already running; stop play mode to apply a fixture (game-agent.py stop)")
            return {
                "ok": True,
                "already_active": True,
                "playing": True,
                "mode": TURN_BASED_MODE,
                "actions": actions,
                "status": status,
            }

        inspect = _inspect_editor(client)
        playing = bool(inspect.get("playing"))
        has_mock = bool(inspect.get("hasMock"))
        mode = inspect.get("mode")
        scene_ok = inspect.get("scene") == "Game_Field" or inspect.get("path") == GAME_FIELD_SCENE

        if playing and has_mock and mode == TURN_BASED_MODE:
            if fixture:
                return _error("Match already running; stop play mode to apply a fixture (game-agent.py stop)")
            return {
                "ok": True,
                "already_playing": True,
                "playing": True,
                "scene": inspect.get("scene"),
                "mode": mode,
                "actions": actions,
                "inspect": inspect,
            }

        if playing:
            actions.append("stop")
            _editor_action(client, "stop")
            client, inspect = _wait_editor(url, playing=False)
            has_mock = bool(inspect.get("hasMock"))
            mode = inspect.get("mode")
            scene_ok = inspect.get("scene") == "Game_Field" or inspect.get("path") == GAME_FIELD_SCENE

        if scene_ok is False:
            actions.append("load_scene")
            load = client.call("manage_scene", {"action": "load", "path": GAME_FIELD_SCENE})
            if isinstance(load, dict) and load.get("success") is False:
                return _error(str(load.get("error") or load.get("message") or load))
            inspect = _inspect_editor(client)
            has_mock = bool(inspect.get("hasMock"))
            mode = inspect.get("mode")

        if has_mock is False:
            return _error(
                f"GameMock not found. Open {GAME_FIELD_SCENE} in the Editor. Inspect: {json.dumps(inspect)}"
            )

        if mode != TURN_BASED_MODE:
            actions.append("set_mode")
            raw = _code_result(
                client.call("execute_code", {"action": "execute", "code": _SET_MODE_CODE})
            )
            try:
                set_result = json.loads(raw)
            except json.JSONDecodeError as error:
                return _error(f"set_mode JSON failed ({error}): {raw[:500]}")
            if isinstance(set_result, dict) is False or set_result.get("ok") is not True:
                return _error(str(set_result.get("error") if isinstance(set_result, dict) else raw))
            inspect = _inspect_editor(client)
            mode = inspect.get("mode")
            if mode != TURN_BASED_MODE:
                return _error(f"Failed to set GameMock._mode to {TURN_BASED_MODE}, still {mode}")

        if fixture:
            actions.append("set_fixture")
            fixture_json = json.dumps(fixture, ensure_ascii=False)
            code = _SET_FIXTURE_CODE % {
                "key": json.dumps(FIXTURE_PREFS_KEY),
                "json": json.dumps(fixture_json),
            }
            raw = _code_result(client.call("execute_code", {"action": "execute", "code": code}))
            if '"ok":true' not in raw.replace(" ", ""):
                return _error(f"set_fixture failed: {raw[:500]}")

        actions.append("play")
        _editor_action(client, "play")
        _, inspect = _wait_editor(url, playing=True, attempts=40)
        if bool(inspect.get("hasMock")) is False:
            return _error(
                "Entered play mode but GameMock is missing. Scene must be Game_Field with GameMock on Root."
            )

        return {
            "ok": True,
            "playing": True,
            "scene": inspect.get("scene"),
            "mode": inspect.get("mode"),
            "fixture": fixture,
            "actions": actions,
            "inspect": inspect,
        }
    except McpError as error:
        return _error(str(error))


def _stop_play(client: McpClient) -> dict:
    try:
        inspect = _inspect_editor(client)
        if bool(inspect.get("playing")) is False:
            return {"ok": True, "playing": False, "already_stopped": True}
        _editor_action(client, "stop")
        _, inspect = _wait_editor(client.url, playing=False)
        return {"ok": True, "playing": False, "inspect": inspect}
    except McpError as error:
        return _error(str(error))


def _fixture_from_params(params: dict) -> dict:
    fixture: dict = {}
    for key, value in params.items():
        field = FIXTURE_FIELDS.get(key)
        if field is None:
            raise McpError(f"Unknown fixture field {key!r}; known: {', '.join(FIXTURE_FIELDS)}")
        if field == "SelfBoardLayout" and isinstance(value, list):
            value = "\n".join(value)
        fixture[field] = value
    return fixture


def _is_observation(payload: object) -> bool:
    return isinstance(payload, dict) and "Self" in payload and "Opponent" in payload


def _is_legal_plays(payload: object) -> bool:
    return isinstance(payload, dict) and "Cards" in payload and "Self" not in payload


def _save_state(payload: object) -> None:
    if _is_observation(payload) is False or payload.get("HasError"):
        return
    CACHE_DIR.mkdir(parents=True, exist_ok=True)
    LAST_STATE_PATH.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")


def _load_state() -> dict | None:
    if LAST_STATE_PATH.is_file() is False:
        return None
    try:
        data = json.loads(LAST_STATE_PATH.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None
    return data if isinstance(data, dict) else None


def _cells_by_position(player: dict) -> dict[tuple[int, int], str]:
    result: dict[tuple[int, int], str] = {}
    for cell in player.get("Cells") or []:
        result[(int(cell["X"]), int(cell["Y"]))] = str(cell.get("Status") or "closed")
    return result


def _effect_counts(player: dict) -> dict[str, int]:
    counts: dict[str, int] = {}
    for cell in player.get("Cells") or []:
        for effect in cell.get("Effects") or []:
            counts[str(effect)] = counts.get(str(effect), 0) + 1
    return counts


def _board_diff(before: dict, after: dict) -> dict:
    old = _cells_by_position(before)
    new = _cells_by_position(after)
    opened: list[list[int]] = []
    closed: list[list[int]] = []
    flagged: list[list[int]] = []
    unflagged: list[list[int]] = []
    exploded: list[list[int]] = []
    for position in sorted(set(old) | set(new)):
        was = old.get(position, "closed")
        now = new.get(position, "closed")
        if was == now:
            continue
        point = [position[0], position[1]]
        if now == "exploded":
            exploded.append(point)
        elif now == "open":
            opened.append(point)
        elif now == "flagged":
            flagged.append(point)
        elif now == "closed" and was == "flagged":
            unflagged.append(point)
        elif now == "closed":
            closed.append(point)
    # Mines / Flags come from the player counters (area attacks plant mines on the enemy
    # board, scouts flag), effects from cell Effects (Fog / Blackout / Frost / Smoke).
    counters = {}
    for key in ("Mines", "Flags"):
        if before.get(key) is not None and before.get(key) != after.get(key):
            counters[key.lower()] = [before.get(key), after.get(key)]
    effects = {}
    old_effects = _effect_counts(before)
    new_effects = _effect_counts(after)
    for name in sorted(set(old_effects) | set(new_effects)):
        if old_effects.get(name, 0) != new_effects.get(name, 0):
            effects[name.lower()] = [old_effects.get(name, 0), new_effects.get(name, 0)]
    return {
        "opened": opened,
        "closed": closed,
        "flagged": flagged,
        "unflagged": unflagged,
        "exploded": exploded,
        "counters": counters,
        "effects": effects,
    }


def _resources_diff(before: dict, after: dict) -> dict:
    return {
        key: [before.get(key), after.get(key)]
        for key in ("Health", "Mana", "ManaMax", "MovesLeft")
    }


def _hand_diff(before: dict, after: dict) -> dict:
    old = {str(card.get("Id")): card.get("Type") for card in before.get("Hand") or []}
    new = {str(card.get("Id")): card.get("Type") for card in after.get("Hand") or []}
    return {
        "removed": [old[card_id] for card_id in old if card_id not in new],
        "added": [new[card_id] for card_id in new if card_id not in old],
    }


def _diff(client: McpClient) -> object:
    before = _load_state()
    if before is None:
        return _error("no previous state")

    after = client.game("game_get_state")
    if isinstance(after, dict) is False or "Self" not in after:
        return after

    before_self = before.get("Self") or {}
    before_opponent = before.get("Opponent") or {}
    after_self = after.get("Self") or {}
    after_opponent = after.get("Opponent") or {}
    events = after.get("Events") or [] if after.get("Sequence") != before.get("Sequence") else []

    result = {
        "sequence": [before.get("Sequence"), after.get("Sequence")],
        "trigger": after.get("Trigger"),
        "is_own_turn": after.get("IsOwnTurn"),
        "game_over": after.get("GameOver"),
        "self": _board_diff(before_self, after_self),
        "opponent": _board_diff(before_opponent, after_opponent),
        "resources": _resources_diff(before_self, after_self),
        "opponent_resources": _resources_diff(before_opponent, after_opponent),
        "hand": _hand_diff(before_self, after_self),
        "events": events,
    }
    _save_state(after)
    return result



# Cards whose Use needs OPEN enemy cells (SelectFree); the rest of the opponent-board
# cards take closed or any cells. Mirrors AgentCardCatalog (shared/Game/Agent).
OPEN_CELL_ATTACKS = {"Trebuchet", "MineCluster", "CarpetBomb", "FortuneBlast", "FogOfWar"}
EVENTS_TAIL = 8
EVENT_WIDTH = 160
BEST_CELLS = 3
SOLVER_LIMIT = 64


class Output:
    """What a command returns: raw payload for --json, text for the agent."""

    def __init__(self, payload: object, text: str | None = None, failed: bool | None = None) -> None:
        self.payload = payload
        self.text = text
        self.failed = _observation_failed(payload) if failed is None else failed


def _load_solver():
    import importlib.util

    path = Path(__file__).resolve().parent / "board-solver.py"
    spec = importlib.util.spec_from_file_location("board_solver", path)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


def _yes(value: object) -> str:
    return "yes" if value else "no"


def _base_type(card_type: str) -> str:
    return card_type[:-4] if card_type.endswith("_Max") else card_type


def _fmt_hand_card(card: dict) -> str:
    card_type = card.get("Type") or "?"
    if card_type == "?":
        return "?"
    target = card.get("Target") or ""
    shape = card.get("Shape") or ""
    size = card.get("Size") or 0
    desc = target
    if shape and shape != "None":
        desc += f" {shape}" + (f"/{size}" if size else "")
    return f"{card_type}({card.get('ManaCost', 0)} {desc.strip()})"


def _fmt_player(label: str, player: dict) -> str:
    return (
        f"{label} hp {player.get('Health')}/{player.get('HealthMax')}"
        f" mana {player.get('Mana')}/{player.get('ManaMax')}"
        f" moves {player.get('MovesLeft')}/{player.get('MovesMax')}"
        f" mines {player.get('Mines')} flags {player.get('Flags')}"
        f" deck {player.get('DeckCount')} stash {player.get('StashCount')}"
    )


def _fmt_board(label: str, player: dict) -> str:
    ascii_board = player.get("BoardAscii") or ""
    if not ascii_board:
        return f"{label}: not generated yet"
    rows = ascii_board.split("\n")
    width = max(len(row) for row in rows)
    ruler = "".join(str(x % 10) for x in range(width))
    lines = [f"{label} {width}x{len(rows)} (x right, y down)", "    " + ruler]
    for y, row in enumerate(rows):
        lines.append(f"{y:2d}  {row}")
    return "\n".join(lines)


# Session log lines worth the agent's attention: card plays of both sides, opened cells,
# health and buffs, game over, skipped turns. Bot flag/eval/state chatter, mana refills,
# board reveal dumps and round markers are dropped: a bot round is ~40 lines and they push
# the one line that matters (`[Bot] Card | Used Medic`) out of the tail.
EVENT_KEEP = ("[Card] Used", "[Card] Effect", "[Bot] Card |", "[Cell] Opened", "[Health]", "[Buff]", "[Game]", "[Turn]")
_EVENT_STAMP = re.compile(r"^\[\d\d:\d\d:\d\d(?:\.\d+)?\] ")


def _event_text(event) -> str | None:
    text = _EVENT_STAMP.sub("", str(event))
    if not text.startswith(EVENT_KEEP):
        return None
    if len(text) > EVENT_WIDTH:
        text = text[: EVENT_WIDTH - 3] + "..."
    return text


def _fmt_events(events: list, limit: int = EVENTS_TAIL) -> list[str]:
    if not events:
        return []
    kept = [text for text in (_event_text(event) for event in events) if text]
    shown = kept[-limit:]
    lines = [f"events ({len(events)} new, {len(shown)} shown):"]
    for text in shown:
        lines.append("  " + text)
    return lines


def _render_observation(observation: dict) -> str:
    lines = []
    if observation.get("HasError"):
        lines.append(f"ERR {observation.get('Error') or 'unknown error'}")
    lines.append(
        f"seq {observation.get('Sequence')} trigger={observation.get('Trigger') or '-'}"
        f" own_turn={_yes(observation.get('IsOwnTurn'))} game_over={_yes(observation.get('GameOver'))}"
    )
    if observation.get("GameOver"):
        lines.append(f"winner {observation.get('WinnerId')} reason: {observation.get('WinReason')}")
    self_view = observation.get("Self") or {}
    opponent = observation.get("Opponent") or {}
    lines.append(_fmt_player("self", self_view))
    lines.append(_fmt_player("opp ", opponent))
    hand = self_view.get("Hand") or []
    lines.append("hand: " + (" ".join(_fmt_hand_card(card) for card in hand) if hand else "empty"))
    # One summary per distinct type: the compact hand line alone let agents invent card
    # conditions (GamblersRuin "needs enemy effects"). The catalog text is the reference.
    seen = set()
    for card in hand:
        card_type = str(card.get("Type") or "")
        if card_type in seen or not card.get("Summary"):
            continue
        seen.add(card_type)
        lines.append(f"  {card_type}: {card.get('Summary')}")
    opponent_hand = opponent.get("Hand") or []
    if opponent_hand:
        lines.append(f"opp hand: {len(opponent_hand)} cards")
    if self_view.get("Modifiers"):
        lines.append("self mods: " + ", ".join(self_view["Modifiers"]))
    if opponent.get("Modifiers"):
        lines.append("opp mods: " + ", ".join(opponent["Modifiers"]))
    lines.extend(_fmt_events(observation.get("Events") or []))
    lines.append("legend: digit=open (mines around) .=closed F=flag *=exploded ~=fog")
    lines.append(_fmt_board("SELF", self_view))
    lines.append(_fmt_board("OPP", opponent))
    return "\n".join(lines)


def _fmt_change(before: dict, after: dict, key: str) -> str:
    old = before.get(key)
    new = after.get(key)
    if old is None or old == new:
        return str(new)
    return f"{old}->{new}"


def _fmt_board_diff(diff: dict) -> str:
    parts = [f"{key} {len(value)}" for key, value in diff.items() if value and isinstance(value, list)]
    for key, (old, new) in (diff.get("counters") or {}).items():
        parts.append(f"{key} {old}->{new}")
    for key, (old, new) in (diff.get("effects") or {}).items():
        parts.append(f"{key} cells {old}->{new}")
    return " ".join(parts) if parts else "no change"


def _card_summary(before: dict | None, card_type: str | None, removed: list[str]) -> str | None:
    """`Type: Summary` of the card that just left the hand: the result line alone does not
    say what Trebuchet / Blackout / DimensionRift did, the catalog summary does."""
    if not before:
        return None
    played = card_type or (removed[0] if removed else None)
    if not played:
        return None
    for card in (before.get("Self") or {}).get("Hand") or []:
        if _base_type(str(card.get("Type") or "")) == _base_type(played):
            summary = card.get("Summary") or ""
            return f"{card.get('Type')}: {summary}" if summary else None
    return None


def _fmt_hand_change(hand: dict, card_type: str | None) -> str:
    """`hand: played X drew A B` / `hand: played X discarded C D`: the branch of a coin-flip
    or draw card is visible without a second `state` call."""
    removed = list(hand["removed"])
    played = None
    if card_type:
        for index, card in enumerate(removed):
            if _base_type(str(card)) == _base_type(card_type):
                played = removed.pop(index)
                break
    if played is None:
        return "hand: " + " ".join([f"-{card}" for card in removed] + [f"+{card}" for card in hand["added"]])
    parts = [f"hand: played {played}"]
    if hand["added"]:
        parts.append("drew " + " ".join(hand["added"]))
    if removed:
        parts.append("discarded " + " ".join(removed))
    return " ".join(parts)


def _action_notes(before_self: dict, self_view: dict, self_diff: dict, opponent_diff: dict, card_type: str | None) -> list[str]:
    """Second-line facts an agent otherwise infers: own mines gone without damage, own board
    reshaped by a card (LEGAL PLAYS best and SOLVER are stale)."""
    notes = []
    mines = (self_diff.get("counters") or {}).get("mines")
    if mines and mines[0] > mines[1]:
        if before_self.get("Health") == self_view.get("Health"):
            notes.append(f"own mines {mines[0]}->{mines[1]} removed without damage (hp {self_view.get('Health')})")
        else:
            notes.append(f"own mines {mines[0]}->{mines[1]} with damage (hp {before_self.get('Health')}->{self_view.get('Health')})")
    flags = (self_diff.get("counters") or {}).get("flags")
    if card_type and flags and flags[0] > flags[1]:
        notes.append(f"own flags {flags[0]}->{flags[1]}: the card opened flagged cells that held no mine (wrong flags removed)")
    changed = ("opened", "closed", "flagged", "unflagged", "exploded")
    if card_type and any(self_diff.get(key) for key in changed):
        notes.append("own board changed by the card: LEGAL PLAYS best and SOLVER are stale, re-run legal-plays / solve")
    if card_type and any(opponent_diff.get(key) for key in changed):
        notes.append("opponent board changed by the card: LEGAL PLAYS best for attack cards is stale, re-run legal-plays before the next one")
    return notes


def _render_action(observation: dict, before: dict | None, card_type: str | None = None) -> str:
    if observation.get("HasError"):
        return f"ERR {observation.get('Error') or 'unknown error'}"
    self_view = observation.get("Self") or {}
    opponent = observation.get("Opponent") or {}
    parts = [f"ok seq {observation.get('Sequence')} own_turn={_yes(observation.get('IsOwnTurn'))}"]
    summary = None
    notes: list[str] = []
    hand = {"removed": [], "added": []}
    if before:
        before_self = before.get("Self") or {}
        before_opponent = before.get("Opponent") or {}
        parts.append(
            f"hp {_fmt_change(before_self, self_view, 'Health')}"
            f" mana {_fmt_change(before_self, self_view, 'Mana')}/{_fmt_change(before_self, self_view, 'ManaMax')}"
            f" moves {_fmt_change(before_self, self_view, 'MovesLeft')}"
        )
        self_diff = _board_diff(before_self, self_view)
        parts.append("self: " + _fmt_board_diff(self_diff))
        opponent_diff = _board_diff(before_opponent, opponent)
        if any(opponent_diff.values()) or before_opponent.get("Health") != opponent.get("Health"):
            parts.append(
                f"opp: hp {_fmt_change(before_opponent, opponent, 'Health')} " + _fmt_board_diff(opponent_diff)
            )
        hand = _hand_diff(before_self, self_view)
        if hand["removed"] or hand["added"]:
            parts.append(_fmt_hand_change(hand, card_type))
        if card_type or hand["removed"]:
            summary = _card_summary(before, card_type, hand["removed"])
        notes.extend(_action_notes(before_self, self_view, self_diff, opponent_diff, card_type))
    else:
        parts.append(f"hp {self_view.get('Health')} mana {self_view.get('Mana')} moves {self_view.get('MovesLeft')}")
    if observation.get("GameOver"):
        parts.append(f"GAME OVER winner {observation.get('WinnerId')} reason: {observation.get('WinReason')}")
    lines = [" | ".join(parts)]
    if summary:
        lines.append(summary)
    lines.extend(notes)
    if before and (hand["removed"] or hand["added"]) and hand["added"]:
        # The turn header is stale after a draw: print the hand as it is now.
        current = self_view.get("Hand") or []
        lines.append("hand now: " + (" ".join(_fmt_hand_card(card) for card in current) if current else "empty"))
    lines.extend(_fmt_events(observation.get("Events") or [], limit=4))
    return "\n".join(lines)


def _render_diff(diff: dict) -> str:
    if diff.get("HasError"):
        return f"ERR {diff.get('Error')}"
    parts = [
        f"seq {diff['sequence'][0]}->{diff['sequence'][1]} trigger={diff.get('trigger') or '-'}"
        f" own_turn={_yes(diff.get('is_own_turn'))} game_over={_yes(diff.get('game_over'))}",
        " ".join(f"{key} {old}->{new}" if old != new else f"{key} {new}" for key, (old, new) in diff["resources"].items()),
        "self: " + _fmt_board_diff(diff["self"]),
        "opp: " + _fmt_board_diff(diff["opponent"]),
    ]
    hand = diff["hand"]
    if hand["removed"] or hand["added"]:
        parts.append("hand: " + " ".join([f"-{c}" for c in hand["removed"]] + [f"+{c}" for c in hand["added"]]))
    lines = [" | ".join(parts)]
    lines.extend(_fmt_events(diff.get("events") or [], limit=6))
    return "\n".join(lines)


def _hand_shapes(observation: dict, solver) -> tuple[tuple[str, int], ...]:
    shapes = dict(solver.DEFAULT_SIZES)
    pairs = set(shapes.items())
    for card in (observation.get("Self") or {}).get("Hand") or []:
        shape = card.get("Shape")
        size = card.get("Size") or 0
        if shape in solver.DEFAULT_SIZES and size > 0:
            pairs.add((shape, size))
    return tuple(sorted(pairs))


def _solve_for_hand(observation: dict, solver) -> dict:
    """Solver result trimmed to the hand: no SCOUT / ZIPZAP / ATTACK TARGETS for cards
    that are not in hand, they were four lines of noise per turn."""
    solved = solver.solve(observation, shapes=_hand_shapes(observation, solver), limit=SOLVER_LIMIT)
    hand = (observation.get("Self") or {}).get("Hand") or []
    own_shapes = any(c.get("Target") == "OwnBoard" and c.get("Shape") in ("Rhombus", "Cross", "Line", "Chain") for c in hand)
    zipzap = any(_base_type(str(c.get("Type") or "")) == "ZipZap" for c in hand)
    attack_shapes = any(c.get("Target") == "OpponentBoard" and c.get("Shape") in ("Rhombus", "Cross", "Line") for c in hand)
    if not own_shapes:
        solved["scout_targets"] = []
    if not zipzap:
        solved["zipzap_targets"] = []
    if not attack_shapes:
        solved["attack_targets"] = []
    return solved


def _rift_best(legal: set, size: int, observation: dict, solver) -> list[str]:
    """DimensionRift swaps the diamond on both boards. Rank centres by what the swap gives
    (opponent open cells) minus what it destroys on the own side (open cells, flags)."""
    own = _cells_by_position(observation.get("Self") or {})
    opp = _cells_by_position(observation.get("Opponent") or {})
    grid = solver._rhombus_grid(size)
    scored = []
    for x, y in legal:
        area = [c for c in solver._pattern_cells(grid, (x, y)) if c in own or c in opp]
        opp_open = sum(1 for c in area if opp.get(c) == "open")
        own_open = sum(1 for c in area if own.get(c) == "open")
        own_flags = sum(1 for c in area if own.get(c) == "flagged")
        scored.append((opp_open - 2 * own_open - 3 * own_flags, x, y, opp_open, own_open, own_flags))
    scored.sort(key=lambda item: (-item[0], item[1], item[2]))
    picked: list[str] = []
    centres: list[tuple[int, int]] = []
    for score, x, y, opp_open, own_open, own_flags in scored:
        if any(abs(x - cx) + abs(y - cy) < size for cx, cy in centres):
            continue
        picked.append(f"{x},{y}(opp open {opp_open}, own open {own_open}, own flags {own_flags})")
        centres.append((x, y))
        if len(picked) >= BEST_CELLS:
            break
    return picked


def _best_cells(
    card: dict,
    hand_card: dict,
    solved: dict,
    opponent_solved: dict | None,
    observation: dict | None = None,
    solver=None,
) -> list[str]:
    """Top solver centers for this card among its legal cells."""
    legal = {(cell["X"], cell["Y"]) for cell in card.get("Cells") or []}
    if not legal:
        return []
    card_type = card.get("Type") or ""
    base = _base_type(card_type)
    target = hand_card.get("Target") or ""
    shape = hand_card.get("Shape") or ""
    size = hand_card.get("Size") or 0

    # Area cards get centres at least `spacing` apart: three neighbouring centres cover
    # the same cells, and the second card played there fails with "No free cells".
    spacing = size if shape in ("Rhombus", "Cross", "Line") else 0

    def take(items: list[dict], note=None) -> list[str]:
        picked = []
        centres = []
        for item in items:
            point = (item["x"], item["y"])
            if point not in legal:
                continue
            if any(abs(point[0] - c[0]) + abs(point[1] - c[1]) < spacing for c in centres):
                continue
            label = f"{item['x']},{item['y']}"
            if note:
                label += note(item)
            picked.append(label)
            centres.append(point)
            if len(picked) >= BEST_CELLS:
                break
        return picked

    if target == "OwnBoard":
        if base == "ZipZap":
            return take(solved.get("zipzap_targets") or [], lambda item: f"({item['unknown']} closed)")
        if shape in ("Rhombus", "Cross", "Line"):
            pool = [t for t in solved.get("scout_targets") or [] if t["shape"] == shape and t["size"] == size]
            return take(pool, lambda item: f"({item['unknown']} unknown)")
        if shape == "Chain":
            pool = [t for t in solved.get("scout_targets") or [] if t["shape"] == "Rhombus"]
            return take(pool, lambda item: f"({item['unknown']} unknown)")
        return []

    if target == "OpponentBoard":
        if base == "DimensionRift" and observation is not None and solver is not None:
            return _rift_best(legal, size, observation, solver)
        if shape in ("Single", "Chain"):
            if not opponent_solved:
                return []
            proven = take(opponent_solved.get("flags") or [], lambda item: "(mine proven)")
            if len(proven) >= BEST_CELLS:
                return proven
            guesses = sorted(opponent_solved.get("guesses") or [], key=lambda item: -item["p_mine"])
            return proven + take(guesses, lambda item: f"(p_mine {item['p_mine']:.2f})")[: BEST_CELLS - len(proven)]
        if shape in ("Rhombus", "Cross", "Line"):
            metric = "open" if base in OPEN_CELL_ATTACKS else "closed"
            pool = [
                t
                for t in solved.get("attack_targets") or []
                if t["shape"] == shape and t["size"] == size and t["metric"] == metric
            ]
            return take(pool, lambda item: f"({item[metric]} {metric})")
    return []


def _render_legal(legal: dict, observation: dict | None, solver=None, solved: dict | None = None) -> str:
    if legal.get("HasError"):
        return f"ERR {legal.get('Error')}"
    hand_by_id = {}
    if observation:
        for card in (observation.get("Self") or {}).get("Hand") or []:
            hand_by_id[str(card.get("Id"))] = card
    opponent_solved = None
    if solver and observation and ((observation.get("Opponent") or {}).get("Cells")):
        opponent_solved = solver.solve(observation, side="opponent", limit=SOLVER_LIMIT)

    lines = [f"LEGAL PLAYS own_turn={_yes(legal.get('IsOwnTurn'))} (best = solver centers among legal cells)"]
    own_generated = bool(((observation or {}).get("Self") or {}).get("Cells"))
    for card in legal.get("Cards") or []:
        card_type = card.get("Type") or "?"
        hand_card = hand_by_id.get(str(card.get("Id"))) or {}
        head = f"  {card_type} cost {card.get('ManaCost', 0)}"
        if card.get("Error"):
            lines.append(f"{head}  ERR {card['Error']}")
            continue
        target = hand_card.get("Target") or ""
        shape = hand_card.get("Shape") or ""
        size = hand_card.get("Size") or 0
        desc = target
        if shape and shape != "None":
            desc += f" {shape}" + (f"/{size}" if size else "")
        parts = [head + (f"  {desc.strip()}" if desc.strip() else "")]
        if card.get("NeedsPosition"):
            cells = card.get("Cells") or []
            parts.append(f"cells {len(cells)}")
            if target == "OwnBoard" and own_generated is False and cells:
                parts.append("own board not generated: the card generates it around the click, nothing to scan yet; open a cell first")
            if cells:
                best = _best_cells(card, hand_card, solved or {}, opponent_solved, observation, solver) if solved is not None else []
                if best:
                    parts.append("best " + " ".join(best))
                else:
                    sample = " ".join(f"{c['X']},{c['Y']}" for c in cells[:BEST_CELLS])
                    parts.append(f"e.g. {sample}")
            else:
                parts.append("no legal cell")
        else:
            # "no position" alone was read as "no target": self cards are playable as they are.
            parts.append("no position needed, playable now")
        if card.get("NeedsExtraCard"):
            ids = card.get("ExtraCardIds") or []
            named = [f"{hand_by_id.get(str(i), {}).get('Type', '?')}={i}" for i in ids]
            parts.append("--extra-card-id one of: " + (" ".join(named) if named else "none"))
        if card.get("NeedsChosenIndex"):
            parts.append("--chosen-index 0-2")
        lines.append("  ".join(parts))
    return "\n".join(lines)


def _turn_ready(observation: dict) -> bool:
    if observation.get("GameOver"):
        return True
    if observation.get("Trigger") == "turn_start":
        return True
    if observation.get("IsOwnTurn") and observation.get("Trigger") == "action":
        return ((observation.get("Self") or {}).get("MovesLeft") or 0) > 0
    return False


def _wait_turn(client: McpClient, timeout_ms: int) -> object:
    """game_wait_turn returns as soon as IsOwnTurn is true, including stale action
    frames with MovesLeft 0 while the round is still closing. Keep polling until a
    frame the agent can act on."""
    deadline = time.monotonic() + timeout_ms / 1000
    first_ms = min(timeout_ms, 60000)
    observation = client.game("game_wait_turn", {"timeout_ms": first_ms}, timeout=max(first_ms // 1000 + 15, 30))
    if _is_observation(observation) is False:
        return observation
    if observation.get("HasError") and "Timed out" not in (observation.get("Error") or ""):
        return observation
    last_sequence = observation.get("Sequence")
    while True:
        if _is_observation(observation) and observation.get("HasError") is not True and _turn_ready(observation):
            return observation
        if time.monotonic() >= deadline:
            return _error(f"Timed out waiting for turn_start after {timeout_ms} ms (last seq {last_sequence})")
        time.sleep(1.0)
        fresh = client.game("game_get_state")
        if _is_observation(fresh) is False:
            return fresh
        if fresh.get("HasError"):
            continue
        observation = fresh
        last_sequence = fresh.get("Sequence")


def _turn(client: McpClient, timeout_ms: int) -> Output:
    observation = _wait_turn(client, timeout_ms)
    if _is_observation(observation) is False or observation.get("HasError"):
        return Output(observation, _render_observation(observation) if _is_observation(observation) else None)
    _save_state(observation)
    text = [_render_observation(observation)]
    payload = {"observation": observation}
    if observation.get("GameOver") or observation.get("IsOwnTurn") is not True:
        return Output(payload, "\n".join(text), failed=False)
    solver = _load_solver()
    solved = _solve_for_hand(observation, solver)
    legal = client.game("game_legal_plays")
    payload["legal"] = legal
    payload["solver"] = solved
    if isinstance(legal, dict):
        text.append(_render_legal(legal, observation, solver, solved))
    text.append("SOLVER")
    text.extend(solver.format_human(solved))
    return Output(payload, "\n".join(text), failed=False)


def _legal_plays(client: McpClient) -> Output:
    legal = client.game("game_legal_plays")
    if isinstance(legal, dict) is False:
        return Output(legal)
    observation = _load_state()
    if observation is None:
        observation = client.game("game_get_state")
        _save_state(observation)
    solved = None
    solver = None
    if _is_observation(observation) and observation.get("HasError") is not True:
        solver = _load_solver()
        solved = _solve_for_hand(observation, solver)
    return Output(legal, _render_legal(legal, observation if _is_observation(observation) else None, solver, solved))


def _solve(client: McpClient, section: str) -> Output:
    """Solver on the current state. `--section flags` answers "any new flags?" in one line
    instead of the full dump before the last move of a round."""
    observation = client.game("game_get_state")
    if _is_observation(observation) is False:
        return Output(observation)
    if observation.get("HasError"):
        return Output(observation, f"ERR {observation.get('Error')}", failed=True)
    _save_state(observation)
    solver = _load_solver()
    solved = _solve_for_hand(observation, solver)
    if section == "flags":
        flags = solved.get("flags") or []
        text = "FLAGS: none" if not flags else f"FLAGS: {len(flags)} " + " ".join(f"{f['x']},{f['y']}" for f in flags)
        return Output(solved, text)
    if section == "opens":
        opens = solved.get("safe_opens") or []
        chords = solved.get("chords") or []
        lines = ["SAFE OPENS: none" if not opens else f"SAFE OPENS: {len(opens)} " + " ".join(f"{o['x']},{o['y']}" for o in opens)]
        lines.append("CHORDS: none" if not chords else f"CHORDS: {len(chords)} " + " ".join(f"{c['x']},{c['y']}" for c in chords))
        return Output(solved, "\n".join(lines))
    return Output(solved, "\n".join(solver.format_human(solved)))


# Actions the server refuses once the round is closing: open / chord need a move, a card
# played at MovesLeft 0 lands in the 0.2 s window before the round closes at best.
TURN_TOOLS = ("game_open", "game_chord", "game_use_card")
CELL_TOOLS = ("game_open", "game_chord", "game_flag", "game_unflag")
NEIGHBOURS = ((-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1))


def _check_target(before: dict, tool: str, x: int, y: int) -> str | None:
    """Validate a cell action against the observation saved after the previous action.
    Agents batch `chord A; chord B; chord C` from one solver list; after a cascade the
    tail is stale and the server refuses it. This refuses it locally, with the reason."""
    self_view = before.get("Self") or {}
    cells = {(int(c["X"]), int(c["Y"])): c for c in self_view.get("Cells") or []}
    if not cells:
        return None
    cell = cells.get((x, y))
    if cell is None:
        return f"{x},{y} is outside the board"
    status = str(cell.get("Status") or "closed")
    hint = f"(state seq {before.get('Sequence')}); run solve for a fresh list"
    if tool == "game_open":
        if status == "open":
            return f"stale: {x},{y} is already open {hint}"
        if status == "flagged":
            return f"{x},{y} is flagged: unflag it first"
    elif tool == "game_chord":
        if status != "open":
            return f"chord needs an open cell, {x},{y} is {status} {hint}"
        around = [cells[(x + dx, y + dy)] for dx, dy in NEIGHBOURS if (x + dx, y + dy) in cells]
        closed = sum(1 for c in around if c.get("Status") == "closed")
        flagged = sum(1 for c in around if c.get("Status") == "flagged")
        if closed == 0:
            return f"stale: {x},{y} has no closed neighbours left {hint}"
        mines_around = int(cell.get("MinesAround") or 0)
        if mines_around != flagged:
            return f"chord {x},{y} needs {mines_around} flag(s) around, {flagged} placed {hint}"
    elif tool == "game_flag":
        if status == "open":
            return f"stale: {x},{y} is already open {hint}"
        if status == "flagged":
            return f"{x},{y} is already flagged"
    elif tool == "game_unflag":
        if status != "flagged":
            return f"{x},{y} is not flagged ({status})"
    return None


def _action(client: McpClient, tool: str, params: dict | None = None, timeout: int = 120) -> Output:
    before = _load_state()
    if tool in TURN_TOOLS and _is_observation(before) and before.get("IsOwnTurn") and not before.get("GameOver"):
        if ((before.get("Self") or {}).get("MovesLeft") or 0) <= 0:
            return Output(_error("MovesLeft is 0: the round is closing, run `turn` before the next action"))
    # Only on own turn: the bot cannot touch this board then, so the saved frame is exact.
    if tool in CELL_TOOLS and _is_observation(before) and before.get("IsOwnTurn") and not before.get("GameOver"):
        problem = _check_target(before, tool, int((params or {}).get("x", 0)), int((params or {}).get("y", 0)))
        if problem:
            return Output(_error(problem))
    card_type = (params or {}).get("type") if tool == "game_use_card" else None
    if card_type and _is_observation(before):
        # Same refusal the server gives; saves a round trip and never lets a card through on an empty pool.
        self_before = before.get("Self") or {}
        for card in self_before.get("Hand") or []:
            if _base_type(str(card.get("Type") or "")) != _base_type(card_type):
                continue
            cost = card.get("ManaCost") or 0
            mana = self_before.get("Mana") or 0
            if mana < cost:
                return Output(_error(f"Not enough mana: {cost} needed, {mana} left"))
            break
    observation = client.game(tool, params or {}, timeout=timeout)
    if _is_observation(observation) is False:
        return Output(observation)
    text = _render_action(observation, before, card_type)
    _save_state(observation)
    return Output(observation, text)


def _end_turn(client: McpClient, timeout_ms: int) -> Output:
    current = client.game("game_get_state")
    if _is_observation(current) and current.get("HasError") is not True and current.get("IsOwnTurn"):
        moves = (current.get("Self") or {}).get("MovesLeft") or 0
        if moves <= 0:
            return Output(_error("MovesLeft is 0: the round closes itself, run `wait` (or `turn`) instead of end-turn"))
    return _action(client, "game_end_turn", {"timeout_ms": timeout_ms}, timeout=max(timeout_ms // 1000 + 15, 30))



def _cluster_http() -> int | None:
    request = urllib.request.Request(CLUSTER_HEALTH_URL, method="GET")
    try:
        with urllib.request.urlopen(request, timeout=5) as response:
            return response.status
    except urllib.error.HTTPError as error:
        return error.code
    except (urllib.error.URLError, OSError):
        return None


def _silo_started() -> float | None:
    import subprocess

    try:
        completed = subprocess.run(["ps", "-o", "lstart=", "-C", "Silo"], capture_output=True, text=True, check=False)
    except OSError:
        return None
    line = completed.stdout.strip().splitlines()
    if not line:
        return None
    try:
        return time.mktime(time.strptime(line[0].strip(), "%a %b %d %H:%M:%S %Y"))
    except ValueError:
        return None


def _changed_sources() -> list[Path]:
    import subprocess

    try:
        completed = subprocess.run(
            ["git", "status", "--porcelain", "--untracked-files=all", "--", *STALE_DIRS],
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )
    except OSError:
        return []
    paths = []
    for line in completed.stdout.splitlines():
        if len(line) < 4:
            continue
        path = line[3:]
        if " -> " in path:
            path = path.split(" -> ", 1)[1]
        paths.append(REPO_ROOT / path.strip('"'))
    return paths


def _preflight(url: str, ignore_stale: bool = False) -> Output:
    """Cluster answers, MCP answers, no backend/shared edits after the Silo started."""
    problems = []
    parts = []

    code = _cluster_http()
    parts.append(f"cluster {code if code is not None else 'unreachable'}")
    if code != 200:
        problems.append(
            f"cluster is not up ({CLUSTER_HEALTH_URL} -> {code if code is not None else 'unreachable'}):"
            " ask the user to start it (start-cluster skill), do not start it yourself"
        )

    status: object = None
    try:
        status = McpClient(url).game("game_status")
        active = isinstance(status, dict) and status.get("active") is True
        parts.append(f"mcp ok match_active={_yes(active)}")
    except McpError as error:
        parts.append("mcp unreachable")
        problems.append(f"Unity MCP unreachable at {url} ({error}): ask the user to connect MCP for Unity, do not start it")

    started = _silo_started()
    stale = []
    if started is None:
        parts.append("silo not found in ps")
    else:
        parts.append("silo started " + time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(started)))
        for path in _changed_sources():
            try:
                mtime = path.stat().st_mtime
            except OSError:
                continue
            if mtime > started:
                stale.append((path, mtime))
        stale.sort(key=lambda item: -item[1])
        if stale:
            newest = stale[0]
            relative = newest[0].relative_to(REPO_ROOT)
            message = (
                f"STALE cluster: {len(stale)} file(s) under {'/'.join(STALE_DIRS)} changed after the Silo started,"
                f" newest {relative} at {time.strftime('%H:%M:%S', time.localtime(newest[1]))}."
                " Ask the user to restart the cluster (start-cluster skill); it plays with old rules until then"
            )
            if ignore_stale:
                parts.append("sources stale (ignored)")
            else:
                problems.append(message)
        else:
            parts.append("sources fresh")

    text = " | ".join(parts)
    if problems:
        text += "\n" + "\n".join("FAIL " + problem for problem in problems)
    payload = {"ok": not problems, "summary": parts, "problems": problems, "status": status,
               "stale": [str(path.relative_to(REPO_ROOT)) for path, _ in stale]}
    return Output(payload, text, failed=bool(problems))


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


def _add_fixture_args(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--scenario", help="Named JSON in tools/scripts/agent-scenarios/, or a .json path")
    parser.add_argument("--board", help="Self board layout DSL (BoardParser alphabet)")
    parser.add_argument("--hand", nargs="+", help="CardType names for the human opening hand")
    parser.add_argument("--deck", nargs="+", help="CardType names for the human draw pile")
    parser.add_argument("--bot-deck", nargs="+", dest="bot_deck", help="CardType names for the bot draw pile")
    parser.add_argument("--bot", choices=["Easy", "Medium", "Hard"], help="Bot difficulty for this match")
    parser.add_argument("--human-first", dest="human_first", action=argparse.BooleanOptionalAction, default=None)
    parser.add_argument("--mana", type=int)
    parser.add_argument("--moves", type=int)


def _xy(args: argparse.Namespace) -> dict:
    return {"x": args.x, "y": args.y}


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Call Unity game_* MCP tools via execute_custom_tool.",
    )
    parser.add_argument("--url", default=DEFAULT_URL, help="MCP HTTP endpoint")
    parser.add_argument("--json", action="store_true", help="Raw JSON payload instead of compact text")
    sub = parser.add_subparsers(dest="cmd", required=True)

    sub.add_parser("status", help="Match active / whose turn")
    preflight = sub.add_parser("preflight", help="Cluster 200, MCP reachable, no backend/shared edits after Silo start")
    preflight.add_argument("--ignore-stale", action="store_true")
    ensure = sub.add_parser(
        "ensure-play",
        help="Set GameMock._mode to LastManStandingTurnBased, write the fixture, enter Play",
    )
    _add_fixture_args(ensure)
    start = sub.add_parser("start", help="preflight + ensure-play + game_start_vs_bot")
    _add_fixture_args(start)
    start.add_argument("--ignore-stale", action="store_true", help="Start even if backend/shared changed after Silo start")
    start.add_argument("--skip-preflight", action="store_true")
    sub.add_parser("stop", help="Exit Play mode (needed before start with a new fixture)")
    sub.add_parser("scenarios", help="List named start scenarios")
    sub.add_parser("diff", help="What changed since the last saved state (state / wait / diff)")
    state = sub.add_parser("state", help="Last observation")
    state.add_argument("--oracle", action="store_true")
    wait = sub.add_parser("wait", help="Wait for turn_start / game over (skips stale frames with MovesLeft 0)")
    wait.add_argument("--timeout-ms", type=int, default=300000)
    turn = sub.add_parser("turn", help="wait + state + legal plays with best cells + solver, in one call")
    turn.add_argument("--timeout-ms", type=int, default=300000)
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
    solve = sub.add_parser("solve", help="Solver on the current state (safe opens, chords, flags, targets for cards in hand)")
    solve.add_argument("--section", choices=["all", "flags", "opens"], default="all", help="flags: one line, any proven flags left")
    inspect = sub.add_parser("inspect", help="Unity cell inspect (not server ASCII)")
    inspect.add_argument("x", type=int)
    inspect.add_argument("y", type=int)
    inspect.add_argument("--opponent", action="store_true")
    wait_visual = sub.add_parser("wait-visual", help="Wait until own-board cell animations finish")
    wait_visual.add_argument("--timeout-ms", type=int, default=5000)
    end_turn = sub.add_parser("end-turn", help="SkipTurn: ends the current own round now; only with MovesLeft > 0, after the last move use wait")
    end_turn.add_argument("--timeout-ms", type=int, default=60000)

    args = parser.parse_args()

    try:
        if args.cmd == "scenarios":
            result: object = _list_scenarios()
        elif args.cmd == "preflight":
            result = _preflight(args.url, args.ignore_stale)
        else:
            if args.cmd == "start" and args.skip_preflight is False:
                checked = _preflight(args.url, args.ignore_stale)
                if checked.failed:
                    result = checked
                    output = result
                    sys.stdout.write((output.text or "") + "\n")
                    return 1
            client = McpClient(args.url)
            result = _dispatch(client, args)
    except McpError as error:
        print(error, file=sys.stderr)
        return 2

    output = result if isinstance(result, Output) else Output(result)
    if args.json or output.text is None:
        _print(output.payload)
    else:
        sys.stdout.write(output.text + "\n")
    return 1 if output.failed else 0


def _dispatch(client: McpClient, args: argparse.Namespace) -> object:
    command = args.cmd
    if command == "status":
        return client.game("game_status")
    if command == "ensure-play":
        return _ensure_play(client, _fixture_from_params(_start_params(args)))
    if command == "start":
        fixture = _fixture_from_params(_start_params(args))
        prepared = _ensure_play(client, fixture)
        if _observation_failed(prepared):
            return prepared
        fresh = _connect(client.url)
        started = fresh.game("game_start_vs_bot")
        if isinstance(started, dict) and fixture:
            started["fixture"] = fixture
        return started
    if command == "stop":
        return _stop_play(client)
    if command == "diff":
        diff = _diff(client)
        return Output(diff, _render_diff(diff) if isinstance(diff, dict) else None)
    if command == "state":
        params = {"oracle": True} if args.oracle else {}
        observation = client.game("game_get_state", params)
        if _is_observation(observation) is False:
            return observation
        _save_state(observation)
        return Output(observation, _render_observation(observation))
    if command == "wait":
        observation = _wait_turn(client, args.timeout_ms)
        if _is_observation(observation) is False:
            return observation
        _save_state(observation)
        return Output(observation, _render_observation(observation))
    if command == "turn":
        return _turn(client, args.timeout_ms)
    if command == "open":
        return _action(client, "game_open", _xy(args))
    if command == "chord":
        return _action(client, "game_chord", _xy(args))
    if command == "flag":
        return _action(client, "game_flag", _xy(args))
    if command == "unflag":
        return _action(client, "game_unflag", _xy(args))
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
        return _action(client, "game_use_card", params)
    if command == "legal-plays":
        return _legal_plays(client)
    if command == "solve":
        return _solve(client, args.section)
    if command == "inspect":
        params = _xy(args)
        if args.opponent:
            params["opponent"] = True
        return client.game("game_inspect_cell", params)
    if command == "wait-visual":
        observation = client.game(
            "game_wait_visual",
            {"timeout_ms": args.timeout_ms},
            timeout=max(args.timeout_ms // 1000 + 15, 30),
        )
        if _is_observation(observation) is False:
            return observation
        return Output(observation, "ok visuals settled" if observation.get("HasError") is not True else f"ERR {observation.get('Error')}")
    if command == "end-turn":
        return _end_turn(client, args.timeout_ms)
    raise McpError(f"unknown command {command}")


if __name__ == "__main__":
    sys.exit(main())
