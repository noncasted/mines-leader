---
name: agent-play
description: Play a LastManStandingTurnBased vs-bot match through Unity MCP using tools/scripts/game-agent.py. Use this skill whenever the user runs /agent-play, asks to play as the agent, start agent play, сыграть агентом, запустить agent play, vs-bot MCP match, or to take turns with game_open / game_use_card / game_end_turn. Do not start the Aspire cluster or Unity MCP — the user brings those up by hand.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /agent-play, agent-play, agent play, сыграй агентом, запусти agent play, играй vs бота, game-agent, game_open, game_start_vs_bot
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Agent Play

Sit in the human seat of a `GameMatchType.LastManStandingTurnBased` (31) vs-bot match and take turns through `tools/scripts/game-agent.py`.

The user starts the Aspire cluster and Unity MCP (Editor window on `:8080`) themselves. Do not run `/start-cluster`. Do not launch Unity. Do not launch `mcp-for-unity` / `uvx mcp-for-unity`. If either is down, stop and say what is missing.

Moves go only through `game-agent.py`. Do not invent Unity MCP `game_*` calls — Coplay HTTP often hides parameterized tools from `tools/list`; the script already falls back to `execute_custom_tool`.

Repo root: `/projects/mines-leader`

```bash
python3 tools/scripts/game-agent.py <cmd>
```

Override URL with `$GAME_AGENT_MCP_URL` (default `http://127.0.0.1:8080/mcp`).

## Arguments

Default: start (or attach) and play until game over.

| User says | Do |
|-----------|----|
| `/agent-play` | Full match |
| `start` / `только старт` / `подключись` | Attach, print `status` + `state`, stop |
| `один ход` / `one turn` | One own turn, then stop |
| `--scenario NAME` | Pass through to `start` (see fixture gap) |
| `--bot Easy\|Medium\|Hard`, `--hand`, `--deck`, `--board` | Pass through to `start` (see fixture gap) |

## Step 1 — Preconditions (fail fast, start nothing)

**Cluster** — only check, never start:

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:7103/api/benchmarks
```

Not `200`: stop. Tell the user to start the cluster themselves (`dotnet run --project backend/Orchestration/Aspire/Aspire.csproj --launch-profile http`). Do not do it.

**Unity MCP HTTP** — only check:

```bash
python3 tools/scripts/game-agent.py status
```

Cannot reach MCP: stop. Tell the user to connect MCP for Unity in the Editor (HTTP `127.0.0.1:8080`). Do not start the server.

`status` JSON with `active: false` (or `HasError` about play mode): Editor is not in a GameMock match. Tell the user, then stop unless Unity Editor MCP tools are **already** connected in this session (see Step 2). Checklist to print:

```
1. Cluster already running
2. Unity MCP window connected
3. Scene Game_Field, GameMock._mode = LastManStandingTurnBased (31)
   (the scene serializes 30 = LastManStanding — that mode has no agent observation)
4. Press Play
```

## Step 2 — Editor play mode (only if Unity MCP tools already exist)

If `search_tool` finds Unity Editor tools (`manage_editor`, `execute_code`, …) **already connected**, use them to get into the match. Still do not launch Unity or the MCP process.

1. Find `GameMock` on `Game_Field`.
2. Set `_mode` to `LastManStandingTurnBased` (enum value `31`) **before** entering play mode. `GameMock.Process` creates the match with whatever `_mode` is at Play.
3. If not playing, enter play mode.
4. If already playing with the wrong mode, exit play mode, set `_mode`, enter again.

If those tools are not connected, skip this step. Do not try to bring Unity MCP up.

## Step 3 — Attach

```bash
python3 tools/scripts/game-agent.py start
```

Timeout / `GameAgentBridge` inactive: the Editor is not in GameMock play mode with type 31. Print the Step 1 checklist and stop.

Then:

```bash
python3 tools/scripts/game-agent.py wait
python3 tools/scripts/game-agent.py state
```

If the user only wanted start, print `status` + `state` and stop.

Observation JSON uses C# PascalCase (`HasError`, `IsOwnTurn`, `GameOver`, `Self`, `Opponent`, `Trigger`). Treat `HasError: true` as a failed command.

### Fixture gap

`start --scenario` / `--board` / `--hand` / `--bot` are forwarded by the CLI, but `game_start_vs_bot` currently ignores them: it only waits for the match `GameMock` already created. Do not claim a scripted board or hand was applied. Named JSON lives in `tools/scripts/agent-scenarios/` for when that path works.

## Step 4 — Play a turn

Repeat until `GameOver`, the user asked for one turn, or 40 own turns (stop and report if the cap hits).

1. `wait` until `IsOwnTurn` or `GameOver`.
2. `state` — read `Self.Health`, `Self.MovesLeft`, `Self.Hand`, `Self.BoardAscii`.
3. Proven minesweeper only:

```bash
python3 tools/scripts/board-solver.py --from-agent --json
```

Solver uses player-visible cells. Ignore `HasMine` even if an oracle leak is present. `card-advisor.py` is optional and needs an OpenCode key — skip it unless the user asked for cards-via-advisor.

4. Act, then re-solve. Do not dump five random guesses in one turn.

Order:

- `flag X Y` for every solver `flags` entry (legal on opponent turn too; do it on own turn).
- `chord X Y` for solver `chords`.
- `open X Y` for solver `safe_opens`.
- After a batch of opens, `wait-visual` before `inspect`.
- If no safe opens/chords/flags and `MovesLeft > 0`, take **one** solver `guesses` (lowest risk). Then go back to `state` + solver. Do not chain guesses.
- Cards: `legal-plays`. Play a card only when the entry has no `Error`, you can pay mana, and it is useful now (Medic if damaged; a scout card with non-empty `cells` when the solver is stuck). `use-card --type Bloodhound --x N --y N`. Skip types whose legal-plays error is `Legal plays not implemented`.
- When `MovesLeft == 0`, or nothing useful remains (no safe action, not taking a guess), `end-turn`.

Refuse to `open` / `chord` / `use-card` / `end-turn` off-turn — the bridge will error `Not your turn`. Flags are allowed off-turn; do not sit on the opponent turn waiting to flag unless you already have proven flags.

If a command returns `HasError`, stop that action, print the error, `state`, and continue the turn if still alive.

## Step 5 — Report

Russian prose. Identifiers in English.

```
## Agent play

Match: active | game over
Winner / WinReason: …
Turns taken: N
Last trigger: turn_start | action | opponent_turn | game_over

What happened:
- flags / opens / chords / cards / guesses this session
- health now
- why we stopped (game over / one turn / precondition / cap)
```

Do not dump full `Cells` arrays. `BoardAscii` plus a short action list is enough.

## Commands

| CLI | MCP tool |
|-----|----------|
| `status` | `game_status` |
| `start` | `game_start_vs_bot` |
| `state` / `state --oracle` | `game_get_state` |
| `wait` | `game_wait_turn` |
| `open X Y` | `game_open` |
| `chord X Y` | `game_chord` |
| `flag X Y` / `unflag X Y` | `game_flag` / `game_unflag` |
| `use-card --type Bloodhound --x N --y N` | `game_use_card` |
| `legal-plays` | `game_legal_plays` |
| `inspect X Y` | `game_inspect_cell` |
| `wait-visual` | `game_wait_visual` |
| `end-turn` | `game_end_turn` |

`--oracle` only works when coordinator `LastManStandingTurnBased.IncludeOracle` is true (default false).

## Do not

- Start Aspire, Docker Postgres, Unity Editor, or the MCP server.
- Use `GameMatchType.LastManStanding` (30) or `TimeLimited` — no agent observation.
- Call `board-solver.py` as if it sent moves. It only suggests.
- Golden screenshots / pixel diff. `inspect` is semantic Unity cell state.
- Wrap `GameCheatsBridge`.
