# Agent Play — spec

Authoritative contract for implementers and critics. If this file and the code disagree, this file wins. Do not invent APIs that are not written here.

Repo root: `/projects/mines-leader`

A model must be able to play a full vs-bot match **step by step**: the server waits with no timer; the model sends the same protocol commands a human client sends; after every own action and after the opponent turn it receives an event log plus a full **player-visible** board.

This is not a live PvP feature. Do not put the new mode in the production menu.

---

## Locked decisions

1. **Only LastManStanding turn-based in this task.** Do not add a TimeLimited turn-based duplicate. TimeLimited without a clock is almost the same mode; do it later if needed.
2. **Do not extract a shared `GameRoundBase` from live rounds.** Copy `LastManStandingRound` into a new class and delete the timer. Do not change the behavior of `TimeLimitedRound` or `LastManStandingRound`.
3. **Do not use `GameMatchType.Single`.** It does not register an `IGameRound`.
4. **Do not change `EmptyResponse`.** Observation is a new one-way `INetworkContext` sent after the existing snapshot.
5. **Player-visible board by default.** Closed cells must not include `HasMine`. Oracle (`IncludeOracle = true` on the observation) is opt-in and only for post-match analysis tools, not for `game_get_state` / action tools.
6. **Flags during the opponent turn stay legal.** Same rules as live LMS. Do not add a server turn-check on `SetFlag` / `RemoveFlag`.
7. **Do not add a server turn-check on `Open` / `CardUse` either** in this task. Live commands do not have one. The MCP tools must refuse off-turn `Open` / `CardUse` / `SkipTurn` on the client bridge. Flags may still be sent off-turn.
8. **Instant bot in this mode only.** `Delay` / `WaitRemainingTime` / the 1.5s start delay become no-ops when `MatchCreateOptions.Type == LastManStandingTurnBased`. Live TimeLimited / LMS bot timing must not change.
9. **Unity MCP tools wrap the protocol**, not mouse clicks and not `execute_code`. The runtime API is `GameAgentBridge` (same pattern as `GameCheatsBridge`). Editor `[McpForUnityTool]` methods are thin wrappers.
10. **Observation is built on the server** from `GameStateCapture` + an in-memory event buffer. Do not assemble the observation from Unity animation state.
11. **Hide opponent hand `CardType`** in the default (non-oracle) observation. The client currently knows the types; the agent must not.
12. **Reuse `LastManStandingRoundRecord`** with `SecondsLeft = 0`. Do not add a new snapshot record type.
13. **Reuse the existing client `LastManStandingRound`** for the new match type. Do not duplicate the client round class. Register the same `IGameRound` + `LastManStandingRoundSnapshotHandler` for `LastManStandingTurnBased`.

---

## Why this exists

The `bot-strength` session analyzed games **after** a human played them, by grepping `backend/.telemetry/logs-games/{date}/{sessionId}.log`. That log has no board, no cascade size, and no remaining-mine count. The timer also makes a model unable to think. This feature closes that loop: the model plays, sees the same information a human sees (plus a structured event delta), and can write the same tables without a human at the keyboard.

---

## Goal (end state)

An agent with Unity in play mode and a running cluster can:

1. Start a vs-bot match of type `LastManStandingTurnBased`.
2. Wait until it is the human's turn (bot may move first; that wait must finish without a wall-clock timer on the server).
3. `open` / `chord` / `flag` / `unflag` / `use_card` / `end_turn`.
4. After each own action: get `{ ok, error, events[], observation }`.
5. After `end_turn`: block until the bot has finished and it is the human's turn again (or the match is over), then get the bot's event log + both boards.

---

## Types

### `GameMatchType`

File: `shared/Domain/GameMatchType.cs`

```csharp
public enum GameMatchType
{
    Single = 10,
    TimeLimited = 20,
    LastManStanding = 30,
    LastManStandingTurnBased = 31,
}
```

### Mode options

File: `shared/Configs/GameModeOptions.cs`

Add a third options object. No `RoundTime`.

```csharp
public LastManStandingTurnBasedModeOptions LastManStandingTurnBased { get; set; } = new();

public class LastManStandingTurnBasedModeOptions
{
    public int PlayerHealth { get; set; } = 3;
    public int PlayerMoves { get; set; } = 5;
    public int PlayerStartMana { get; set; } = 1;
    public int MaxManaCap { get; set; } = 10;
    public int CardMovesCost { get; set; } = 0;
    public bool IncludeOracle { get; set; } = false;
}
```

`GetCardMovesCost` must handle `LastManStandingTurnBased` → `LastManStandingTurnBased.CardMovesCost`. The existing `_ => LastManStanding.CardMovesCost` fallback is not enough once the console writes a distinct value.

`IncludeOracle` is a match-config flag. Default `false`. When `true`, observation cells that are `Taken` include `HasMine`. Action MCP tools still request the player-visible view; only an explicit `game_get_state(oracle=true)` may return oracle data, and only if this flag is true. If the flag is false, `oracle=true` is ignored (still player-visible). This prevents accidental mine leaks.

### Config JSON

File: `backend/Orchestration/Coordinator/config.gameMode.json`

Add:

```json
"LastManStandingTurnBased": {
  "$type": "Shared.LastManStandingTurnBasedModeOptions, Shared",
  "PlayerHealth": 3,
  "PlayerMoves": 5,
  "PlayerStartMana": 4,
  "MaxManaCap": 10,
  "CardMovesCost": 0,
  "IncludeOracle": false
}
```

### Console

File: `backend/Console/Game/Configs/GameModeConfigEditor.razor`

Add a third card **Last Man Standing (Turn Based)** with the fields above (no Round Time). Do not remove the two existing cards.

---

## Slice 1 — server round + instant bot

### New round

New file: `backend/Game/GamePlay/Context/Rounds/LastManStandingTurnBasedRound.cs`

Copy `LastManStandingRound.cs`. Then:

- Class name `LastManStandingTurnBasedRound`, logger `ILogger<LastManStandingTurnBasedRound>`.
- `ModeOptions` → `_modeOptions.Value.LastManStandingTurnBased`.
- **Delete `TimerCountdown` entirely.** `ProcessRound` must `await TurnsCountdown()` only (plus the existing try/catch). Do not call `Task.WhenAny` with a timer.
- `TurnsCountdown` stays: poll `player.Moves.Left > 0` every 200ms until skip or moves exhausted.
- `SkipTurn()` stays: log + terminate `_roundForcedLifetime`.
- `IsGameOver` / `GetWinner` / `GetWinReason`: **no time-bank / timeout branch.** Same HP / disconnect / flag-after-2-rounds rules as LMS.
- `EmitRoundSnapshot` still sends `RecordLastManStandingRound(currentPlayer, currentRound, secondsLeft: 0)`.
- `GetMovesMax` stays (bot `MovesPerRound` override).
- Init snapshot uses `RecordLastManStandingRound` with `secondsLeft: 0`.
- Do not send `TimeLimitedRoundRecord`.

### Registration

File: `backend/Game/Global/SessionFactory.cs`

In **both** `CreateMatch` and `CreateMatchWithBot` switches:

```csharp
case GameMatchType.LastManStandingTurnBased:
    services.Add<LastManStandingTurnBasedRound>()
            .As<IService>()
            .As<IGameRound>();
    break;
```

`Single` stays empty. `TimeLimited` / `LastManStanding` stay as they are.

### Instant bot

`MatchCreateOptions` is already in the session container. Inject it into `BotProfileBase` (and therefore Easy/Medium/Hard).

In `Delay`, `DelayForAction`, `WaitRemainingTime`, and the 1.5s start delay inside `ExecuteTurn`:

- if `_matchOptions.Type == GameMatchType.LastManStandingTurnBased` → `return Task.CompletedTask` (or skip the `await Delay(...)` call).
- otherwise keep current timing.

Do not add a new bot profile. Do not change `MinRoundTime` / `MaxRoundTime` in `config.bot.json`.

### Tests (required)

New file: `backend/Tools/Tests/Game/LastManStandingTurnBasedRoundTests.cs`

Minimum:

1. `GetCardMovesCost_TurnBased_UsesOwnOptions` — set `LastManStanding.CardMovesCost = 1`, `LastManStandingTurnBased.CardMovesCost = 0`, assert `GetCardMovesCost(LastManStandingTurnBased) == 0`.
2. `ProcessRound_NoTimerLoop` — the new round file must not contain `TimerCountdown` or `TimeSpan.FromSeconds(1)` used as a turn clock. Critic greps the file. A test that reflects/reads source is optional; the critic will grep.
3. `BotDelay_TurnBased_IsInstant` — call the delay helper (extract `BotProfileBase` delay to a testable `internal static`/`public` function if needed, e.g. `BotTurnTiming.ShouldSkipDelay(GameMatchType)`) and assert `true` only for `LastManStandingTurnBased`.

Run:

```bash
dotnet test backend/Tools/Tests/Tests.csproj -- --filter-class "*LastManStandingTurnBased*" --filter-class "*BotTurnTiming*"
```

If you extract `BotTurnTiming`, filter that class too. After tests, convert logs with `tools/scripts/get-test-log.sh` if you need to read failures.

### Acceptance checklist — slice `round`

- [ ] `GameMatchType.LastManStandingTurnBased = 31`
- [ ] `LastManStandingTurnBasedModeOptions` exists, no `RoundTime`
- [ ] `GetCardMovesCost` has an explicit arm
- [ ] `config.gameMode.json` has the new section
- [ ] Console editor has the third card
- [ ] `LastManStandingTurnBasedRound` exists and has **no** `TimerCountdown`
- [ ] `SessionFactory` registers it on **both** create paths
- [ ] Live `TimeLimitedRound` / `LastManStandingRound` still have their timers
- [ ] Bot delays are skipped only for this match type
- [ ] Required tests exist and pass
- [ ] `progress.md` snapshot row for `round` updated

---

## Slice 2 — observation

### Event buffer

New file: `backend/Game/Session/Logging/IObservationEventBuffer.cs` (+ impl next to it)

```csharp
public interface IObservationEventBuffer
{
    int Cursor { get; }
    void Append(string line);
    IReadOnlyList<string> TakeAfter(int exclusiveCursor);
}
```

- `Append` stores the **same text** `SessionFileLogger` writes after the timestamp (or the full `[HH:mm:ss.fff] ...` line — pick one and use it everywhere; prefer the full line so it matches the session log).
- `TakeAfter` returns lines with cursor `>` `exclusiveCursor`, in order. Does not clear the buffer (so two subscribers can drain independently if they keep their own cursor). Bound memory to the last **2000** lines; drop oldest.
- `SessionFileLogger.Write` must also `Append` to the buffer.
- Register the buffer as a singleton in session services (`AddSessionServices`).

### Extra events the old log was missing

When a cell reveal / card reveal opens **N** cells, append one extra line:

```
[Board] Revealed | Player={Label} | Count={N} | Positions=(x,y),(x,y),...
```

Call this from `MoveSnapshotBoardExtensions.RecordReveal` (after `Revealer.Reveal`) via `ISessionLogger` or the buffer. If the reveal is empty, do not write the line.

`LogCardUsed` stays. Do not invent other new event kinds in this task.

### Observation DTO

New file: `shared/Game/Agent/SharedAgentObservation.cs`

Register in `SharedGameExtensions.AddSharedGame` with `builder.Add<SharedAgentObservation>()`.

```csharp
[MemoryPackable]
public partial class SharedAgentObservation : INetworkContext
{
    public int Sequence { get; set; }
    public int EventCursor { get; set; }
    public bool IsOwnTurn { get; set; }
    public bool GameOver { get; set; }
    public Guid WinnerId { get; set; } // Guid.Empty if none
    public string WinReason { get; set; }
    public string Trigger { get; set; } // "action" | "turn_start" | "opponent_turn" | "game_over"
    public bool HasError { get; set; }
    public string Error { get; set; }
    public List<string> Events { get; set; }
    public AgentPlayerView Self { get; set; }
    public AgentPlayerView Opponent { get; set; }
}

[MemoryPackable]
public partial class AgentPlayerView
{
    public Guid Id { get; set; }
    public int Health { get; set; }
    public int HealthMax { get; set; }
    public int Mana { get; set; }
    public int ManaMax { get; set; }
    public int MovesLeft { get; set; }
    public int MovesMax { get; set; }
    public bool MovesAvailable { get; set; }
    public int Mines { get; set; }
    public int Flags { get; set; }
    public List<string> Modifiers { get; set; }
    public List<AgentCardView> Hand { get; set; }
    public int DeckCount { get; set; }
    public int StashCount { get; set; }
    public string BoardAscii { get; set; }
    public List<AgentCellView> Cells { get; set; }
}

[MemoryPackable]
public partial class AgentCardView
{
    public Guid Id { get; set; }
    public string Type { get; set; } // CardType name, or "?" if hidden
    public int ManaCost { get; set; } // 0 if hidden
}

[MemoryPackable]
public partial class AgentCellView
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Status { get; set; } // "closed" | "open" | "flagged" | "exploded"
    public int MinesAround { get; set; } // meaningful only when Status == "open"
    public List<string> Effects { get; set; }
    public bool HasMine { get; set; } // only set when oracle or Status == "exploded"
}
```

All lists must be non-null (empty, not null).

### ASCII board

16×16 (or actual generated size). Header row of x indices is optional. Body:

| Glyph | Meaning |
|-------|---------|
| `.` | closed, not flagged |
| `F` | flagged (closed) |
| `0`–`8` | open, mines-around |
| `*` | exploded mine |
| `~` | closed + Fog or Blackout (still no `HasMine`) |

One line per y, no spaces between cells. Example:

```
....1F..
.111F...
```

`BoardAscii` is this grid. `Cells` is the structured form of the same information.

### Builder

New file: `backend/Game/GamePlay/Agent/AgentObservationBuilder.cs`

```csharp
public static class AgentObservationBuilder
{
    public static SharedAgentObservation Build(
        IGameContext context,
        Guid viewerId,
        IReadOnlyList<string> events,
        string trigger,
        bool oracle,
        bool hasError,
        string error);
}
```

Rules:

- `Self` is the player with `viewerId`. `Opponent` is the other one. If `viewerId` is missing, leave both empty and set `HasError`.
- `IsOwnTurn` = `IGameRound.CurrentPlayer.Value?.User.Id == viewerId` (inject round or pass `currentPlayerId`).
- For each board, iterate every cell in `GameStateCapture` / live `IBoard`.
  - `Free` + exploded (if you can detect explosion; otherwise `Free` with mine detonated via existing board API) → `open` or `exploded`.
  - `Taken` + flagged → `flagged`.
  - `Taken` + not flagged → `closed`.
  - `MinesAround` only for `open`.
  - `HasMine` only if `oracle == true` **or** `Status == "exploded"`.
  - Fog/Blackout on a closed cell → ASCII `~`, `Effects` contains the type name.
- Opponent `Hand`: if `oracle == false`, each card is `{ Id, Type = "?", ManaCost = 0 }`. Keep `Id` so later tools can still talk about slots if needed; do not leak `CardType`.
- Self `Hand`: real `CardType` name + `ManaCost` from `ICardConfigs` (inject or pass). `Id` is the hand card id (required for `game_use_card`).
- `Modifiers`: `PlayerModifier` names with value ≠ 0, or `DurationalModifierOverview` type names. Stable string, one per source.
- `Mines` / `Flags` from board counters if they exist; otherwise count cells.
- `Sequence`: monotonic int on the publisher, not on the builder.

### Publisher

New file: `backend/Game/GamePlay/Agent/IAgentObservationPublisher.cs` (+ impl)

```csharp
public interface IAgentObservationPublisher
{
    void Publish(Guid viewerId, string trigger, bool hasError, string error);
}
```

- No-op unless `MatchCreateOptions.Type == LastManStandingTurnBased`.
- Drains `TakeAfter(_lastCursor[viewerId])` (store per-viewer cursor; first call uses `-1`).
- Builds via `AgentObservationBuilder` with `oracle: modeOptions.IncludeOracle` **only when trigger is a dedicated oracle request**. For `"action"`, `"turn_start"`, `"opponent_turn"`, `"game_over"` always pass `oracle: false`. (Oracle is MCP `game_get_state` only; see slice 3. Server publisher used after gameplay is always player-visible.)
- `user.Send(observation)` to that viewer only (not `SendAll`). Bots have a no-op connection; do not send to bots.
- Increment `Sequence` per send.

### When to publish

1. **After every `GameCommand`** (including cheats and `SkipTurn`): in `GameCommand.Execute`, after `SnapshotSender.Send`, call `Publish(player.User.Id, "action", response.HasError, response.Message)`. Only if publisher is registered (turn-based session). Inject `IAgentObservationPublisher` into `GameCommandUtils` as optional or always-registered no-op for other modes.
2. **Start of a player's `ProcessRound`** (after restore-moves snapshot): `Publish(player.User.Id, "turn_start", false, null)` — so the human gets a state dump when their turn begins, including everything the bot just did if this is the human's turn after the bot.
3. **After `ProcessRound` returns** (end of that player's turn): publish to the **opponent** with trigger `"opponent_turn"`. This is how the human sees the bot's turn as one bundle if they were waiting. If you already publish `turn_start` to the next player, `opponent_turn` to the waiting human is still required so `game_end_turn` can complete before the next `turn_start` if the client is only watching the opponent trigger. **Implement both.** `game_end_turn` completes on the first observation where `IsOwnTurn == true` or `GameOver == true`.
4. **Game over**: after `RecordGameCompleted` / `LogGameOver`, publish to the human with `GameOver = true`, `WinnerId`, `WinReason`, trigger `"game_over"`.

Publisher must be registered in `AddGameContext` (or session services) for every match; implementation no-ops when type is not turn-based. That way `GameCommandUtils` can always take it.

`GameCommandUtils` currently **requires** `IGameRound`. Turn-based registers one. Do not break TimeLimited / LMS.

### Tests (required)

New file: `backend/Tools/Tests/Game/AgentObservationBuilderTests.cs`

1. Closed taken cell with a mine → `HasMine == false` when `oracle == false`.
2. Same cell → `HasMine == true` when `oracle == true`.
3. Open safe cell → `Status == "open"`, `MinesAround` set, ASCII digit.
4. Opponent hand types are `"?"` when `oracle == false`.
5. Self hand types are real names.
6. `TakeAfter` on the buffer returns only new lines.

Use existing `BoardParser` / substitutes like `OpenCellCommandTests`. Do not stand up Orleans.

### Acceptance checklist — slice `observation`

- [ ] `SharedAgentObservation` (+ nested types) exist and are registered in `AddSharedGame`
- [ ] `IObservationEventBuffer` is appended from `SessionFileLogger`
- [ ] Reveal count line is written
- [ ] Builder never leaks `HasMine` on closed cells when `oracle == false`
- [ ] Builder hides opponent `CardType` when `oracle == false`
- [ ] Publisher no-ops unless match type is `LastManStandingTurnBased`
- [ ] Publish happens after `GameCommand`, on `turn_start`, on `opponent_turn`, on `game_over`
- [ ] Observation is sent only to the viewer, not `SendAll`
- [ ] Required tests exist and pass
- [ ] Live modes still compile and still use `EmptyResponse`
- [ ] `progress.md` snapshot row for `observation` updated

---

## Slice 3 — client + MCP tools

### Client match type

File: `client/Assets/GamePlay/Loop/PvP/PvPScopeExtensions.cs`

```csharp
case GameMatchType.LastManStanding:
case GameMatchType.LastManStandingTurnBased:
    builder.Register<LastManStandingRound>()
           .As<IGameRound>()
           .As<ILastManStandingRound>();
    builder.AddSnapshotHandler<LastManStandingRoundSnapshotHandler, LastManStandingRoundRecord>();
    break;
```

Do not add a new client round class. `SecondsLeft = 0` is fine on the existing UI.

`GameMock`: `_mode` is already `GameMatchType`. No code change required beyond the enum existing. Do not add the mode to `MenuPlay`.

### Observation on the client

New: `client/Assets/GamePlay/Agent/AgentObservationHandler.cs`

A `OneWayCommand<SharedAgentObservation>` (same pattern as other one-way session commands). On execute: `GameAgentBridge.OnObservation(context)`.

Register it wherever other gameplay one-way commands are registered (follow `GameCompletedSnapshotHandler` / network command add). If one-way commands are auto-discovered via DI, register like `GameCheatsService`.

### `GameAgentBridge`

New: `client/Assets/GamePlay/Agent/GameAgentBridge.cs` (runtime, `GamePlay` assembly)

Static, same lifetime as `GameCheatsBridge` (`Set` on setup, `Clear` on lifetime end).

Wired from a `GameAgentService : IScopeSetup` registered in `PvPScopeExtensions.Construct` (all PvP scopes, including mock).

Holds: `INetworkConnection`, `IGameContext`, `IGameRound`, last `SharedAgentObservation`, a wait handle for new observations (UniTaskCompletionSource or event).

Public API (names exact):

```csharp
public static bool IsActive { get; }
public static SharedAgentObservation LastObservation { get; }

public static UniTask<SharedAgentObservation> WaitObservation(int afterSequence, int timeoutMs);
public static UniTask<SharedAgentObservation> Open(int x, int y);
public static UniTask<SharedAgentObservation> Chord(int x, int y);
public static UniTask<SharedAgentObservation> Flag(int x, int y);
public static UniTask<SharedAgentObservation> Unflag(int x, int y);
public static UniTask<SharedAgentObservation> UseCard(Guid cardId, int? x, int? y, Guid? extraCardId, int? chosenIndex);
public static UniTask<SharedAgentObservation> EndTurn(int timeoutMs);
public static SharedAgentObservation GetState(bool oracle);
```

Behavior:

- `Open` / `Chord` / `Flag` / `Unflag` / `UseCard` / `EndTurn` send the matching `SharedGameAction.*` via `_connection.Request`.
- `Open` / `Chord` / `UseCard` / `EndTurn`: if `IGameRound.IsTurnAllowed == false` and the match is not over, **do not send**. Return a synthetic observation (`HasError = true`, `Error = "Not your turn"`) without hitting the network.
- `Flag` / `Unflag`: always send (off-turn flags are legal).
- After a successful send, `WaitObservation(LastObservation.Sequence, timeoutMs: 10000)` and return that. If timeout, `HasError = true`, `Error = "Timed out waiting for observation"`.
- `EndTurn`: send `SkipTurn`, then wait until an observation arrives with `IsOwnTurn == true` or `GameOver == true` (ignore intermediate `action` from the skip itself if `IsOwnTurn` is still false). Timeout default **60000** ms.
- `GetState(false)` returns `LastObservation` (or a client-side rebuild only if last is null — prefer last server observation).
- `GetState(true)`: if the last observation is all you have, return it; do **not** invent mines from the client board. Oracle is server-side. For v1, `GetState(true)` may return last observation and set `Error = "oracle requires IncludeOracle and a server rebuild"` unless you add a small request. **Preferred:** add `SharedAgentObservationRequest : INetworkContext` (empty) handled by a new `GameCommand` that publishes an observation to the requester with `oracle: mode.IncludeOracle` and trigger `"action"`. `GetState(true)` sends that request and waits. If `IncludeOracle` is false, server still sends player-visible.

### Card payload factory

New: `client/Assets/GamePlay/Agent/CardUsePayloadFactory.cs` (or `shared/` if you can construct payloads without Unity types — **prefer `shared/Game/Agent/CardUsePayloadFactory.cs`** so tests can live in backend).

```csharp
public static class CardUsePayloadFactory
{
    public static ICardUsePayload Create(
        CardType type,
        Position? position,
        Guid? extraCardId,
        int? chosenIndex);
}
```

- Always set `Type = type` on the payload.
- If the payload type implements `IBoardCardUsePayload`, `position` is required; missing → throw `ArgumentException("position required")`.
- `Recycler` → `DiscardCardId = extraCardId` (required).
- `Salvage` → `ChosenIndex = chosenIndex ?? 0`.
- `ZipZap` → `Position` + `CardId = extraCardId ?? Guid.Empty` (set `CardId` to the used card id from the caller if extra is null — `UseCard` should pass the played `cardId` as ZipZap.CardId).
- Every other payload: `new CardUsePayload.{Name} { Type = type }` via a switch on `CardType`. Unknown type → throw.

Keep the switch exhaustive over current `CardType` values. When a new card is added later, compile break is acceptable.

Tests: `backend/Tools/Tests/Game/CardUsePayloadFactoryTests.cs` — Bloodhound requires position; Medic does not; Recycler requires extra id.

### MCP tools

New editor file: `client/Assets/GamePlay/Editor/Agent/GameAgentMcpTools.cs`

Discover the real attribute by grepping the Unity MCP package (`McpForUnityTool`, `McpPluginTool`, or the project's custom-tool docs). Use that attribute. If the package is missing from the tree, still write the static methods with `[McpForUnityTool]` as used by Coplay (`Packages/com.coplaydev.unity-mcp` / skill `execute_custom_tool`). Do not block the slice on a missing package: `GameAgentBridge` is the contract; tools are wrappers.

Tool names (exact):

| Tool | Args | Calls |
|------|------|--------|
| `game_start_vs_bot` | none | See below |
| `game_status` | none | `{ active, isOwnTurn, gameOver, sequence }` from bridge / context |
| `game_get_state` | `oracle?: bool` | `GetState(oracle)` |
| `game_wait_turn` | `timeout_ms?: int` | wait until `IsOwnTurn \|\| GameOver` |
| `game_open` | `x, y` | `Open` |
| `game_chord` | `x, y` | `Chord` |
| `game_flag` | `x, y` | `Flag` |
| `game_unflag` | `x, y` | `Unflag` |
| `game_use_card` | `card_id` (guid string), `x?`, `y?`, `extra_card_id?`, `chosen_index?` | `UseCard` |
| `game_end_turn` | `timeout_ms?: int` | `EndTurn` |

Each mutating tool returns the observation as JSON (Newtonsoft or `JsonUtility` + a serializable DTO). Include `hasError`, `error`, `events`, `self`, `opponent` (ascii + hand + resources). Do not return Unity objects.

`game_start_vs_bot`:

- If `GameAgentBridge.IsActive` and a match is running → return current status (do not start a second match).
- If play mode is running and `IMatchmaking` can be resolved from the live scope → `CreateGameWithBot(LastManStandingTurnBased)` and wait until `GameAgentBridge.IsActive` or timeout 30s.
- If play mode is **not** running → return error `"Enter play mode with GameMock (mode LastManStandingTurnBased) and a running cluster"`. Do not call `EditorApplication.isPlaying = true` in v1 (domain reload races). Document this in `progress.md`.

`GamePlay.Editor.asmdef` already references GamePlay. If the MCP attribute lives in another editor assembly, add that reference. Do not create a reference cycle.

### Acceptance checklist — slice `client-mcp`

- [ ] `PvPScopeExtensions` accepts `LastManStandingTurnBased` without `default` throw
- [ ] No new client `IGameRound` class
- [ ] `GameAgentBridge` + `GameAgentService` wired like cheats
- [ ] `CardUsePayloadFactory` exhaustive + tests
- [ ] Bridge refuses off-turn open/card/skip; allows off-turn flag
- [ ] `EndTurn` waits for `IsOwnTurn` or `GameOver`
- [ ] Ten MCP tool names exist as documented wrappers
- [ ] Mutating tools return observation JSON, not `"ok"`
- [ ] `MenuPlay` unchanged
- [ ] `progress.md` snapshot row for `client-mcp` updated

---

## Style

Follow `.agents/docs/CODE_STYLE_FULL.md`, `.agents/docs/API_DESIGN_FULL.md`, `.agents/docs/TESTING.md`, `.agents/docs/GAMEPLAY.md`:

- braces on the same line
- member order: ctor → readonly fields → mutable fields → public methods → private methods → local functions
- fields `_camelCase`, no abbreviations
- no `Async` suffix on `UniTask` methods
- collections: never return null lists
- backend file-scoped namespaces as in neighboring files
- tests: xUnit v3, FluentAssertions, NSubstitute; no Orleans cluster unless you already have a fixture you must use — **do not** add a full-match cluster test in this task

---

## Forbidden

- Changing timer behavior of `TimeLimitedRound` or `LastManStandingRound`
- Registering `IGameRound` for `Single`
- Putting `HasMine` of closed cells into default observation
- Putting opponent `CardType` into default observation
- Driving play by simulating clicks / `GameInput`
- Using `execute_code` as the product API
- Changing `EmptyResponse` shape
- Extracting a shared base from the two live rounds
- Adding TimeLimited turn-based
- Adding the mode to the production play menu
- Bot-vs-bot
- Headless WebSocket client (out of scope; protocol must stay usable for one later)
- Rewriting `ISessionLogger` format in a breaking way (you may **add** lines)

---

## Slices (workflow)

| Id | Title | Depends on |
|----|--------|------------|
| `round` | Enum, options, console, `LastManStandingTurnBasedRound`, factory, instant bot, tests | — |
| `observation` | Buffer, DTO, builder, publisher, command/turn hooks, tests | `round` |
| `client-mcp` | Client switch, bridge, payload factory, MCP tools | `observation` |

Implement **only** the current slice. Do not start a later slice. A critic must not fail the current slice for work that belongs to a later one.

---

## Key files (read before editing)

| File | Why |
|------|-----|
| `backend/Game/GamePlay/Context/Rounds/LastManStandingRound.cs` | copy source |
| `backend/Game/GamePlay/Context/Rounds/TimeLimitedRound.cs` | do not break |
| `backend/Game/Global/SessionFactory.cs` | register round |
| `backend/Game/GamePlay/Commands/Common/GameCommand.cs` | publish after send |
| `backend/Game/GamePlay/Commands/Common/GameCommandUtils.cs` | inject publisher |
| `backend/Game/Session/Logging/SessionFileLogger.cs` | buffer append |
| `backend/Game/GamePlay/Bot/Profiles/BotProfileBase.cs` | instant delay |
| `backend/Game/GamePlay/Snapshots/GameStateCapture.cs` | builder input |
| `backend/Game/GamePlay/Snapshots/MoveSnapshotBoardExtensions.cs` | reveal count |
| `shared/Domain/GameMatchType.cs` | enum |
| `shared/Configs/GameModeOptions.cs` | options |
| `shared/Game/SharedGameExtensions.cs` | union register |
| `shared/Game/SharedGameAction.cs` | commands to wrap |
| `shared/Game/Cards/ICardUsePayload.cs` | payload factory |
| `client/Assets/GamePlay/Loop/PvP/PvPScopeExtensions.cs` | client switch |
| `client/Assets/GamePlay/Cheats/UIToolkit/GameCheatsWindow.cs` | bridge pattern |
| `client/Assets/GamePlay/Boards/Root/BoardActions.cs` | protocol senders |
| `client/Assets/Tools/Overall/GameMock.cs` | vs-bot entry |
| `backend/Console/Game/Configs/GameModeConfigEditor.razor` | console |
| `backend/Orchestration/Coordinator/config.gameMode.json` | config |
| `backend/Tools/Tests/Game/OpenCellCommandTests.cs` | test style |
