# Agent Play — progress

## Snapshot

| Slice | Status | Notes |
|-------|--------|-------|
| 1 round | done | server LMS turn-based + instant bot |
| 2 observation | done | event buffer + SharedAgentObservation + publisher |
| 3 client-mcp | done | bridge + MCP protocol tools |

## Log

<!-- implementers append below -->

### slice `round`

- Added `GameMatchType.LastManStandingTurnBased = 31`.
- Added `LastManStandingTurnBasedModeOptions` (no `RoundTime`) and an explicit `GetCardMovesCost` arm.
- Added `config.gameMode.json` section and console card **Last Man Standing (Turn Based)**.
- New `LastManStandingTurnBasedRound`: copy of LMS with `TimerCountdown` removed; `ProcessRound` awaits `TurnsCountdown()` only; snapshots use `LastManStandingRoundRecord` with `secondsLeft: 0`.
- `SessionFactory` registers the new round on both `CreateMatch` and `CreateMatchWithBot`.
- Injected `MatchCreateOptions` into `BotProfileBase` / Easy / Medium / Hard. `Delay`, `DelayForAction`, `WaitRemainingTime` (and therefore the 1.5s start delay) no-op when type is `LastManStandingTurnBased` via `BotTurnTiming.ShouldSkipDelay`.
- Live `TimeLimitedRound` / `LastManStandingRound` timers unchanged.
- Tests: `LastManStandingTurnBasedRoundTests` + `BotTurnTimingTests` — 4 passed (`dotnet test backend/Tools/Tests/Tests.csproj -- --filter-class "*LastManStandingTurnBased*" --filter-class "*BotTurnTiming*"`).

### slice `observation`

- Added `IObservationEventBuffer` / `ObservationEventBuffer` (stable cursors, last 2000 lines). Registered as session singleton. `SessionFileLogger.Write` appends the full `[HH:mm:ss.fff] ...` line.
- Reveal cascade now also logs `[Board] Revealed | Player={Label} | Count={N} | Positions=(x,y),...` from `RecordReveal` via `ISessionLogger.LogBoardRevealed` (empty reveal is skipped). `MoveSnapshot.SessionLogger` is set from `GameCommand` and `BotCommandUtils`.
- Added `SharedAgentObservation` + nested views; registered in `AddSharedGame`.
- `AgentObservationBuilder` builds player-visible boards (no closed `HasMine` unless oracle; opponent hand `Type` is `?` unless oracle). ASCII: `.` / `F` / `0-8` / `*` / `~`.
- `AgentObservationPublisher` no-ops unless `LastManStandingTurnBased`. Sends only to the viewer (skips bots). Sequence is monotonic per send. Gameplay triggers always `oracle: false`.
- Publisher does **not** take `IGameRound` in the ctor (that cycled with `LastManStandingTurnBasedRound` via `BuildServiceProvider`). Current player is resolved lazily from `IServiceProvider` inside `Publish`.
- Publish hooks: after `GameCommand` snapshot (`action`); turn-based `ProcessRound` start (`turn_start`); after `ProcessRound` to the opponent (`opponent_turn`, `IsOwnTurn` for the waiting player); `GameFlow` after `RecordGameCompleted` (`game_over`).
- `EmptyResponse` unchanged. Live `TimeLimitedRound` / `LastManStandingRound` timers unchanged.
- Tests: `AgentObservationBuilderTests` — 8 passed, plus `LastManStandingTurnBased*` and `OpenCellCommand*` still green (`dotnet test backend/Tools/Tests/Tests.csproj -- --filter-class "*AgentObservation*" --filter-class "*LastManStandingTurnBased*" --filter-class "*OpenCellCommand*"`).

### slice `client-mcp`

- Client `PvPScopeExtensions` registers the existing `LastManStandingRound` + snapshot handler for both `LastManStanding` and `LastManStandingTurnBased`. No new client round class. `MenuPlay` unchanged.
- `GameAgentService` + static `GameAgentBridge` wired like cheats (`Set` on setup, `Clear` on lifetime end) in every PvP scope including mock.
- `AgentObservationHandler` is a `OneWayCommand<SharedAgentObservation>` registered next to `SnapshotReceiver`.
- Bridge sends `SharedGameAction.*` via `_connection.Request`. Off-turn `Open` / `Chord` / `UseCard` / `EndTurn` return `HasError` + `"Not your turn"` without a network send. `Flag` / `Unflag` always send.
- `EndTurn` sends `SkipTurn` then waits until `IsOwnTurn || GameOver` (default 60000 ms). Action sends wait on the next observation (10000 ms).
- Preferred oracle path: `SharedAgentObservationRequest` + `RequestAgentObservationCommand` (`RequestOracle = true`). Publisher uses `mode.IncludeOracle` only for that request; gameplay publishes stay player-visible. `game_get_state(oracle=true)` awaits `RequestOracleState`.
- `CardUsePayloadFactory` in `shared/Game/Agent` maps every current `CardType` (including `_Max`). Tests: Bloodhound needs position, Medic does not, Recycler needs extra id.
- Ten editor wrappers in `GameAgentMcpTools` as Coplay **classes** (`HandleCommand(JObject)`) with `[McpForUnityTool("game_open")]` etc. Mutating tools return the `SharedAgentObservation` object (hasError/error/events/self/opponent), not `"ok"`. Stub attribute is compiled only when `com.coplaydev.unity-mcp` is absent (`MCP_FOR_UNITY`).
- `game_start_vs_bot` does **not** enter play mode (`EditorApplication.isPlaying = true` is v1-forbidden). If play mode is off: `"Enter play mode with GameMock (mode LastManStandingTurnBased) and a running cluster"`. If a `GameMock` is already in the scene, it does not call `CreateGameWithBot` again (GameMock starts the match). If play mode is running without GameMock and `IMatchmaking` resolves, it creates `LastManStandingTurnBased` and waits up to 30s for `GameAgentBridge.IsActive`.
- Tests: `CardUsePayloadFactoryTests` + `AgentObservation*` + `LastManStandingTurnBased*` — 18 passed (`dotnet test backend/Tools/Tests/Tests.csproj -- --filter-class "*CardUsePayloadFactory*" --filter-class "*AgentObservation*" --filter-class "*LastManStandingTurnBased*"`).

### critic fix `observation`

- Broke ctor cycle: `AgentObservationPublisher` no longer injects `IGameRound` (the turn-based round injects the publisher). `Publish` reads `CurrentPlayer` via `IServiceProvider.GetService<IGameRound>()` after construction.

### critic fix `client-mcp`

- `EndTurn` now ignores the skip's `action` echo (`Trigger == "action"`), even when `IsOwnTurn` is still true (`SkipTurn` only terminates the round lifetime). Completes on `opponent_turn` / `turn_start` (`IsOwnTurn`) or `game_over`.
- `game_start_vs_bot` no longer drops `CreateGameWithBot`'s `MatchResult`. It loads PvP by invoking `IMenuPlay.MatchFound` when the menu is waiting, else `IGamePlayLoader.Load`, else `IGameLoopScopeLoader` + `LoadPvp` + `IPvPGameLoop.Process`. GameMock path still only waits (GameMock already calls `LoadPvPMock`).
