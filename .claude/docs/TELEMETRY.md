# Telemetry & Logging

All telemetry data is stored in `backend/.telemetry/` at the project root (gitignored).

## Directory Structure

```
backend/.telemetry/
  metrics/          — service metrics snapshots (JSON)
  logs/             — backend file logs (all levels including Trace)
  logs-games/       — game session logs (per-session files)
```

## Metrics (`backend/.telemetry/metrics/`)

**Writer:** `tools/framework/backend/Infrastructure/Metrics/MetricsSnapshotService.cs`

- Hosted service, writes every 10 seconds
- **Development only:** in other environments `StartAsync` does nothing (metrics go through OpenTelemetry); in
  Development no repository root (`.git`) fails the service start
- Listens to `MeterListener` for "Backend" meter
- Output: `metrics_{serviceName}.json` (e.g. `metrics_silo.json`, `metrics_metagateway.json`)
- Contains counters, histograms with min/max/avg/sum

## Backend File Logs (`backend/.telemetry/logs/`)

**Writer:** `tools/framework/backend/Infrastructure/Logging/FileLoggerProvider.cs`

- Registered by `ServiceDefaultsExtensions.AddFileLogging` **only in the Development environment** (Aspire);
  production logs go through OpenTelemetry only
- Options `FileLoggerOptions`, configuration section `FileLogging`: `Enabled` (default true), `Directory` (default
  `backend/.telemetry/logs` of the repository found via `.git`; no repository and no `Directory` -> the service fails
  at startup), `MinLevel` (default Trace, applied on top of the `Logging` filters)
- Log calls only enqueue a line; one background writer appends and flushes when its queue is drained, on host
  shutdown and on process exit / unhandled exception
- Output: `{serviceName}.log` (e.g. `silo.log`, `metagateway.log`)
- Format: `[timestamp] [TRC/DBG/INF/WRN/ERR/CRT] [category] message`
- Logs go to both Aspire (OpenTelemetry) and file simultaneously

## Game Session Logs (`backend/.telemetry/logs-games/`)

**Writer:** `backend/Game/Session/Logging/SessionFileLogger.cs`

- Created per game session, disposed on session end
- Output: `{date}/{sessionId}.log` (e.g. `2026-04-13/{guid}.log`)
- Logs game events: cell opens, flags, card usage, health/mana changes, bot actions, round start/end, game over

## Shared Utility

`tools/framework/backend/Infrastructure/TelemetryPaths.cs` — shared helper:
- `FindProjectRoot()` — walks up from `AppContext.BaseDirectory` looking for `.git` (a directory, or a
  `gitdir:` file in a git worktree / submodule)
- `GetTelemetryDir(subfolder)` — returns `backend/.telemetry/{subfolder}`, creates directory if needed; `null`
  without a repository root (session logs then fall back to `logs/sessions` next to the binary)
- `RequireTelemetryDir(subfolder)` — same path, but throws without a repository root (metrics snapshots)

## Reading from the backend: what and how

Three kinds of data live on the backend, not in `backend/.telemetry`; an agent reads them itself over GET of the meta
(MetaGateway), all under the token `CONSOLE_TOKEN` (header `Authorization: Bearer <token>`, `RequireConsoleToken()` in
`backend/Orchestration/MetaGateway/Program.cs`, the same token as the console):

| Data | API | Skill | Contract |
|------|-----|-------|----------|
| Client telemetry: sessions, samples, marks, errors, startup traces | `/api/client-telemetry/...` | `client-telemetry` (a link into the framework) | `tools/framework/backend/ClientTelemetry/Contract/CONTRACT.md` |
| Match metrics: raw series and events of the players and of the server | `/api/match-metrics/...` | - (read through `match-history`) | `tools/framework/backend/MatchMetrics/Contract/CONTRACT.md` |
| Match history: records of the matches with the players from the metrics | `/api/match-history/...` | `match-history` | `backend/Orchestration/MetaGateway/Matches/CONTRACT.md` |

- Production: `https://meta.minesleader.xyz`, the token in `~/.config/mines-leader/deploy.env` (`CONSOLE_TOKEN=...`). Both
  are the section `## client-telemetry` of `.claude/project-skills.md`; the two skills read it. The scripts read only the line
  of the token and send it only in the header: never print, source or pass the env file yourself.
- Local cluster (Aspire): MetaGateway `http://localhost:7101`, the token `local-dev-token` (`ConsoleToken` of
  `backend/Orchestration/Aspire/appsettings.json`); in Development the development token turns the check off
  (`backend/Orchestration/Extensions/ConsoleAccess.cs`), so a 401 is seen only on prod. The skills take
  `--url http://localhost:7101 --env-file <file with CONSOLE_TOKEN=local-dev-token>` after the subcommand.

## Client telemetry

The framework module `telemetry` over the meta socket (`MetaBackend` is the `ITelemetrySocket`;
`AddSocketClientTelemetry(new ClientTelemetryOptions())` and `AddStandardTelemetryCollectors()` in
`client/Assets/Meta/MetaScopeExtensions.cs`). Intake: `AddClientTelemetryCommands()` in MetaGateway, the sessions in the Silo
(`AddClientTelemetrySessions()`), reading `MapClientTelemetryRead()` in MetaGateway and `AddClientTelemetryReader()` in
ConsoleGateway. Off until the flag of the client telemetry on the Features page of the console: on prod it is turned on by hand.
Reading: skill `client-telemetry` (`.claude/skills/client-telemetry/SKILL.md`).

- Sessions (`client/Assets/Common/Flow/Loop/GameLoop.cs`): tag `menu` and tag `match`. The first `menu` opens when the meta
  socket is authorized (`IMetaState.IsAuthorized`: the menu loads in parallel with the meta, a request before the
  authorization is not allowed); every next session opens before its part loads, and closes the previous one. A rematch
  opens a new `match` session. The mark `match.started` (`GamePlay/Telemetry/MatchTelemetryMark`, on `IGameStarted`) splits
  the loading and the wait for the players from the play.
- Startup trace: `MetaLoop` sends `SendStartupTrace` after every authorization of the meta socket (a reconnect too), before
  the first session. Its spans are the `GameProfiler` spans of the scopes and of the meta; on the web the page spans of
  `client/Assets/WebGLTemplates/Default/index.html` (`window.startupTrace`) come first. `$T startups`, `$T startup <id>`.
  `BackendTraceId` of the record is the trace of the root span `Meta connect` of that socket (MetaGateway, source
  `MetaGateway.Connection`, `UserFlow/Connection/MetaConnectTrace.cs`: read request, auth, send response, projections): find it
  by the id in the Aspire dashboard -> Traces.
- Frame: `enableFrameTimingStats` is on in `ProjectSettings.asset` and in the build profile `web_dev - Desktop - Development`
  (a profile keeps its own copy of the value).
- In the editor the telemetry is off by default (`ClientTelemetryOptions`): a local check needs a WebGL build or the option
  turned on for the time of the check.
- `$T monitor --tag match` waits for the next match, `$T sessions --tag menu` lists the menus.

## Match metrics

The game data of a match: counters, gauges and events of each player and of the server, by match id. The collector is the
framework module `match-metrics`: model, sending, intake, storage, read API - `tools/framework/.claude/docs/MATCH_METRICS.md`;
the wire, limits and routes - `tools/framework/backend/MatchMetrics/Contract/CONTRACT.md`. Always on online (no flag, not the
client telemetry above).

- **Match id** = the id of the game session (`SessionId` of its clients, the key of `IMatch`). A rematch is a new session:
  a new id, a new clock from 0, new streams.
- **Clock of the match** (the axis of the series and events of the clients): one for both players, the server time since the
  start of the game of the session - `Game.GamePlay.IMatchClock` / `MatchClock`, stamped on every snapshot
  (`SharedMoveSnapshot.MatchMs`, `SnapshotSender.Send`); the client runs its `IMatchMetricsClock` from it by the real time and
  freezes it at the end of the game. `MatchMs` is 0 before the start.
- **Names** (the contract of the game metrics): `backend/Meta/Matches/Players/MatchGameMetrics.cs` - a change of a name is a
  change there and in the client writers. Names `[a-z0-9._]`, tag values `[a-z0-9._-]` up to 32.

Series and events of a player (the client of the player writes only its own player, one writer class per feature):

| Name | Kind | Tags / fields | Meaning |
|---|---|---|---|
| `cells.opened` | counter | `source` = `click` / `cascade` / `card` | opened cells of the own board |
| `cascades` | counter | - | openings that started a cascade |
| `flags.set`, `flags.removed` | counter | - | flags |
| `mines.hit` | counter | - | a mine blown |
| `cards.played` | counter | `card` | cards played |
| `turns` | counter | - | turns started (turn-based mode) |
| `turn.end` | event | `number`, `seconds` (length of the turn), `cells`, `cards` | end of the own turn |
| `card.play` | event | `card`, `board` (`own` / `enemy`), `cells` (the touched cells, when known) | a card played |
| `match.end` | event | `result` = `win` / `lose` / `leave`, `seconds` | the outcome on the client (no draw in the game) |

`card` is the name of `CardType` in lower case (`Trebuchet_Max` -> `trebuchet_max`). A field the client does not know is
left out; the name stays.

How the client counts (the writers of `client/Assets/GamePlay/Metrics`, fed by whole snapshots at their receive):
- `turns` / `turn.end` in all three modes; a turn is the span of the own player in the round record (the round number
  alone is not a turn: the server raises it at the end of a turn); `number` is the ordinal of the own turn from 1; an open turn is closed at the
  end of the match or at its ending.
- `source` of `cells.opened`: an empty cell (no mines around) and a neighbour of an empty cell of the same action are
  `cascade`, the rest `click` (at least one); a chord with empties may count a part of the direct cells as `cascade`.
- `cells.opened{card}`: cells of the own board opened by a card of any player (opened cells of the card, Dimension Rift
  and Mirror Match included).
- `mines.hit`: only an explosion on the own board by a click or a chord (with a shield too); cells blown by cards are not
  counted.
- `card.play.cells`: the number of the target cells; a card without a target has no field. An unknown card type throws.

Server stream: `Game.Session.SessionMetrics` (`backend/Game/Session/Metrics`, `ISessionMetrics`; started and ended by
`GameFlow`; `AddMatchMetricsWriter()` in `ProjectsSetupExtensions`, `Pass<IMatchMetricsWriter>` in
`SessionFactory`), user `00000000-...`: gauge `session.users` (connected humans, bots never connect), events
`session.join {index}`, `session.leave {index, seconds}` (the end of the session is not a leave), `session.end {seconds,
winner_index}` (-1 without a winner). Its axis is the server time since the open of the stream; the `seconds` of its fields
are of the clock of the match.

Backend wiring: MetaGateway `Program.cs` - `AddMatchMetricsCommands()` (intake over the meta socket),
`AddMatchMetricsAdmission<MatchMetricsAdmission>()` (`backend/Meta/Matches/MatchMetricsAdmission.cs`: a user may write
metrics of a match while it is a participant of its record `IMatch` and the match is not closed), `AddMatchMetricsReader()`,
`MapMatchMetricsRead().RequireConsoleToken()`; ConsoleGateway - `AddMatchMetricsReader()`. Tables `match_metric_*`:
`MatchMetricsSetup` of `backend/Tools/DeploySetup` (from `PostResourcesSetup`).

Where to look:
- One match whole, its players, turns and cards: skill `match-history`, the console `/match/{id}`.
- Raw series, totals across matches, events of the server stream: GET `/api/match-metrics/...` of the meta (routes in the
  contract).
- Charts on the local cluster: Grafana below.

## Match history

The records of the real matches, with the players from the match metrics above. Read API:
`backend/Orchestration/MetaGateway/Matches/CONTRACT.md`.

- **Record**: the grain `Meta.Matches.Match` (`IMatch`, `MatchState`, key = the id of the game session). The only writer is
  the game server (`Game.GamePlay.GameFlow`): the setup at the start of a round (the type, the participants with the deck
  and the nickname of each), the end with a winner (`OnComplete`: winner, rating changes) or without one (`Close`: the round
  ended without a winner, or the session ended during it). Bots are participants.
- **Lists**: every record is in memory of the host in `IMatchSummaryCollection` (a moment after the grain); the history of a
  user is a filter over it.
- **Players of a match** (totals, cells by source, cards by type, events, series by steps, csv): `backend/Meta/Matches/Players`
  (`MatchPlayerCards`, `MatchPlayerSeriesReader`, `MatchPlayerSeriesCsv`) over `IMatchMetricsReader`; one code for the API and
  the console. A participant whose client wrote nothing (a bot, an old client) has `streams: 0`.
- **API** `/api/match-history/...` in MetaGateway (`MapMatchHistoryRead().RequireConsoleToken()`): `matches` (filters
  `player`, `type`, `winner`, `from`, `to`, `closed`, `offset`, `limit`), `matches/{id}`, `matches/{id}/players`,
  `matches/{id}/players/{userId}/series` and `series.csv` (`step` 1 / 10 / 60, `axis` run / server), `players/{nickname|id}`.
- **Console** (`backend/Console/Game`): `/match-history` (list), `/match/{id}` (the record and the players),
  `/match/{id}/players/{userId}` (the series of a player). `/matches` is the page of the live matches, not the history.
- **Skill** `match-history` (`.claude/skills/match-history`): `matches`, `match`, `player`, `series`, `export`, `analyze`
  (`--last N` - the cards played and in decks against the outcome). Its tests:
  `python3 -m unittest discover -s .claude/skills/match-history/tests`.

## Match metrics: Grafana (dev only)

The local cluster (Aspire) starts a `grafana` container (`backend/Orchestration/Aspire/Startup/GrafanaFactory.cs`) over the
same Postgres as the tables `match_metric_*`. Never deployed; off by `"Grafana": { "Enabled": false }` in
`backend/Orchestration/Aspire/appsettings.json` (on by default locally).

- Open `http://localhost:9434` (the Postgres port 9432 + 2): the home dashboard is `Match metrics`; anonymous view without a
  password, `admin` / `admin` to edit.
- Provisioning in `backend/Orchestration/Aspire/ContainersData/Grafana/provisioning`: the datasource `Postgres` (address and
  password from the env set by the AppHost) and `dashboards/match_metrics.json` (change the file, the container rereads it).
- Variables: `match_id` (the last 200 matches), `user_id` (multi), `series` (any series name), `group` (a tag of the series
  or `none`), `step` (s). Panels: the series by the group over the match, its totals by the group, the totals per player.
- The time axis is the clock of the match: `1970-01-01 00:00 UTC` = the start of the game, the default range is its first
  30 min. The server stream (`user 00000000-...`) is on the server time since its open.
