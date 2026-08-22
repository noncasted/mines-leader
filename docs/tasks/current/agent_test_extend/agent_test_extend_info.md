---
task: agent_test_extend
status: done
phase: implementation
created: 2026-08-21
updated: 2026-08-21
total_steps: 5
completed_steps: [1, 2, 3, 4, 5]
blocked_steps: []
---

# Agent Test Extend — spec

Authoritative contract. If this file and the code disagree, this file wins. Do not invent API that is not written here.

Repo root: `/projects/mines-leader`

---

## Что я хочу

Agent play уже умеет ходить протоколом (`game_open` / `game_use_card` / `game_end_turn`) и видит ASCII-доску. Этого мало для тестов: агент **играет, пока не выпадет** нужная рука и мина.

Нужно расширить агента так, чтобы он мог **собрать сценарий и проверить карту/клетку** без удачи:

1. Стартовать LMS turn-based vs-bot из фикстуры: доска (тот же DSL, что `BoardParser` / `BoardLayoutParser`) + рука человека + человек ходит первым.
2. Играть карту по **имени типа** (`Bloodhound`), не только по guid.
3. Спросить сервер: **куда эту карту вообще можно ткнуть** (правила карты, не `board-solver.py`).
4. Спросить клиент: **что реально на клетке в Unity** (state / flag / minesAround / играет ли анимация) — semantic dump, не полноэкранный png.

**Many matches / bot-vs-bot / batch 200 партий — не делаем.** Visual golden frames / pixel diff — не делаем. Live-читы посередине матча (обернуть `GameCheatsBridge`) — не основной путь; фикстура задаётся при **создании** матча.

---

## Цель

Агент с Unity play mode + кластер может:

1. `game_start_vs_bot` с опциональной фикстурой → детерминированная доска и рука.
2. `game_use_card type=Bloodhound x y` без парсинга guid, если в руке одна такая карта.
3. `game_legal_plays` → список карт в руке + легальные клетки / extra-card ids.
4. `game_inspect_cell x y` → Unity-view, не observation с сервера.
5. `game_wait_visual` → пока на своём поле не играет cell animation (иначе inspect врёт).

Observation по-прежнему player-visible: закрытая мина из фикстуры **не** течёт в `HasMine`, пока `oracle=true` и `IncludeOracle`.

---

## Контекст

- Сделано: `docs/tasks/complete/agent_play.md`. Режим `GameMatchType.LastManStandingTurnBased = 31`. MCP в `GameAgentMcpTools.cs`. Bridge: `GameAgentBridge`.
- Сейчас бот ходит первым: `LastManStandingTurnBasedRound` после ready делает `_currentPlayer.Set(botPlayer)`. Для тестов карт это ломает сценарий — фикстура должна уметь `humanGoesFirst: true`.
- Доска ленивая: `EnsureGenerated` no-op, если `board.Cells.Count != 0`. Фикстура заполняет существующий `IBoard` игрока → Generate не перезапишет мины.
- DSL уже production-ready: `backend/Game/GamePlay/Boards/BoardLayoutParser.cs` (тот же алфавит, что `Tests.Game.BoardParser`). Сейчас он **создаёт новый** `Board`. Нужен `Apply(IBoard existing, string layout)`.
- `BoardOptions.Size` задаёт клиентскую сетку (16×16 в сцене). Фикстура **не** меняет размер. Короткий layout паддим `t`. Длиннее size — ошибка.
- Рука: `RoundPlayers.RestoreCards` добирает до `HandSize` из колоды. Фикстурная рука должна **заменить** результат RestoreCards у человека, иначе в руке окажутся лишние карты.
- Читы `GameCheatContexts.CardAdd` уже умеют выдать карту; для старта матча не используем чит-команды — применяем фикстуру внутри round init.
- `game_use_card` сейчас парсит только guid. `CardUsePayloadFactory` уже есть.
- Карты **не** имеют `GetTargets`. `ICard<T>.Use` просто падает (`Bloodhound`: `"No taken cells in the pattern"`). Legal plays — отдельный builder, не python solver.
- `CellAnimator` / `CellView`: анимация open ~0.8s. Observation приходит сразу. Inspect без wait_visual снимает ещё закрытую клетку.
- Не зависит от `prefab_catalog`. Можно делать параллельно.

---

## Locked decisions

1. Only `LastManStandingTurnBased`. Do not add TimeLimited turn-based. Do not put the mode in the production menu.
2. Fixture is match-create, not a mid-game cheat command.
3. Default observation stays player-visible. Planted mines must not leak via `HasMine` on closed cells.
4. Do not wrap the full `GameCheatsBridge` surface in this task (`add_card` mid-match, set HP, end match).
5. Do not do many matches, bot-vs-bot, ActionDelay=0 batch, headless websocket.
6. Do not do pixel Game View screenshots / golden frames. Semantic inspect only. Optional png is out of scope.
7. Legal plays come from **server card rules**, not `tools/scripts/board-solver.py`.
8. `game_start_vs_bot` still does **not** enter play mode. Same error if Editor is not playing.
9. Live `TimeLimitedRound` / `LastManStandingRound` behaviour unchanged.
10. `EmptyResponse` unchanged.

---

## Fixture DTO

New type in `shared/` (MemoryPack, used on the create-match path):

```csharp
[MemoryPackable]
public partial class AgentMatchFixture
{
    public string SelfBoardLayout { get; set; } = string.Empty;
    public List<CardType> SelfHand { get; set; } = new();
    public bool HumanGoesFirst { get; set; } = true;
    public int? Mana { get; set; }
    public int? Moves { get; set; }
}
```

Empty layout → do not touch the board (lazy generate stays). Empty hand → keep RestoreCards. `HumanGoesFirst` default **true** for this DTO; when fixture is null, keep current behaviour (bot first).

Pad/validate layout in `BoardLayoutParser.Apply`:

- Read `board.Size`.
- Parse rows with the existing alphabet: `t m f g _ x`.
- `x` is allowed and treated as `t` (target marker is for tests, not runtime).
- If a row is shorter than size, pad `t`. If any row/col exceeds size → throw `ArgumentException`.
- Write cells onto the **existing** board via `SetCell` / `SetMine` / `ToFree` / `SetFlag` (same sequence as `Parse`). Call `MinesScanner.Recalculate`.
- After Apply, `board.Cells.Count != 0` so `EnsureGenerated` is a no-op.

Do not replace `IPlayer.Board` with a new instance — the player already holds the Board reference, client already built CellViews by size.

Hand apply (human only, after `RestoreCards` in round init):

- Remove every card currently in `player.Hand`.
- `Hand.Add` each `SelfHand` type in order (same as `CardAddCheat`).
- Record snapshot card remove/add so the client hand matches.
- Do not draw from deck for those slots.

First player:

- If fixture is null: keep `_currentPlayer.Set(botPlayer)` when a bot exists.
- If fixture != null && `HumanGoesFirst`: set current player to the non-bot.
- If fixture != null && `HumanGoesFirst == false`: keep bot first.

Mana/Moves: if set, `SetCurrent` after the mode-options init block (so they override start mana/moves).

---

## Transport

Thread the optional fixture through the existing vs-bot create path. Do not add a second match type.

| Layer | File | Change |
|-------|------|--------|
| Client command | `shared/Backend/SharedMatchmaking.cs` `CreateWithBot` | add `AgentMatchFixture Fixture` |
| Client API | `BackendEndpoints.CreateGameWithBot`, `IMatchmaking.CreateGameWithBot` | pass fixture |
| Meta | `MatchmakingCommands.CreateWithBot`, `IMatchFactory.CreateWithBot` | pass fixture |
| Pipe | `MatchPayloads.Match.RequestWithBot` | add Fixture (`[Id(2)]`) |
| Session | `MatchCreateOptions` | add `AgentMatchFixture Fixture` |
| Factory | `SessionFactory.CreateMatchWithBot` | store on options (already registers `MatchCreateOptions`) |

`MatchCreateOptions` is currently `{ Type }`. Add Fixture. Round and player factory already inject `MatchCreateOptions`.

Orleans `[GenerateSerializer]` on `RequestWithBot`: new field **must** have a new `[Id(n)]`. Do not reuse Id 0/1.

Production `CreateWithBot` callers pass `Fixture = null`. Only the MCP start path fills it.

---

## MCP tools

Keep Coplay wrappers as classes + `[McpForUnityTool]`. Mutating tools still return `SharedAgentObservation` JSON except inspect/legal_plays (they return their own DTO).

### `game_start_vs_bot` (extend)

New optional parameters (all optional; omitted = today's start):

| Arg | Type | Meaning |
|-----|------|---------|
| `board` | string | `SelfBoardLayout` (multiline DSL, spaces between cells like BoardParser) |
| `hand` | string[] | CardType names, e.g. `["Bloodhound"]` |
| `human_goes_first` | bool | default true **when any fixture field is present**, else unused |
| `mana` | int? | |
| `moves` | int? | |

If any fixture field is present, send `AgentMatchFixture`. If none, `Fixture = null` (bot-first, random board).

Do not enter play mode. Same 30s wait on `GameAgentBridge.IsActive`.

Update `tools/scripts/game-agent.py` with `--board`, `--hand`, `--human-first`.

### `game_use_card` (extend)

Accept **either** `card_id` (guid) **or** `type` (CardType name, including `_Max`).

- If `type` is set: find the first hand card with that type (from `LastObservation.Self.Hand` or live hand). Zero matches → error `"Card type not in hand"`. Two+ matches → use the first, do not fail.
- `card_id` wins if both are set.
- Position / extra_card_id / chosen_index unchanged.
- Extra card may also be a type name later — **out of scope**. Still guid.

### `game_legal_plays` (new)

No args. Returns JSON, not a full observation:

```json
{
  "isOwnTurn": true,
  "cards": [
    {
      "id": "...",
      "type": "Bloodhound",
      "manaCost": 3,
      "needsPosition": true,
      "needsExtraCard": false,
      "needsChosenIndex": false,
      "cells": [{"x": 3, "y": 4}, {"x": 4, "y": 4}],
      "extraCardIds": []
    },
    {
      "id": "...",
      "type": "Medic",
      "needsPosition": false,
      "cells": [],
      "extraCardIds": []
    }
  ]
}
```

Built **on the server** (new request `SharedAgentLegalPlaysRequest` / response), not guessed on the client. Client tool sends the request and waits, same as oracle.

Rules for the builder (`AgentLegalPlaysBuilder` in `backend/Game/GamePlay/Agent/`):

- Only the viewer's hand.
- `needsPosition` iff payload is `IBoardCardUsePayload`.
- `needsExtraCard` iff Recycler (and ZipZap extra is optional — still expose `needsExtraCard: true` for ZipZap, `extraCardIds` = other hand ids or self).
- `needsChosenIndex` iff Salvage.
- For board cards: enumerate every cell on the **target board** (own board unless the card is cross-board — then opponent board). Include a cell if running the **same selection** the card uses would not be empty. Reuse `PatternShapes` / `SelectTaken` like `Bloodhound.Use`. Do **not** call `Use` (that mutates).
- Exhaustive switch on `CardType` like `CardUsePayloadFactory`. Unknown / not implemented → `cells: []` and `error` on that card entry, do not crash.
- v1 minimum that tests must lock: `Medic` (no cells), `Bloodhound` (taken cells whose rhombus contains at least one taken cell — i.e. any taken cell is legal if the pattern can hit taken; simplest correct rule: a position is legal if `PatternShapes.Rhombus(size).SelectTaken(board, pos).Count > 0`), `Recycler` (`extraCardIds` = other cards in hand).

Oracle mines must **not** appear in this payload.

### `game_inspect_cell` (new)

Args: `x`, `y`, optional `opponent: bool` (default self board).

Runs in the Unity editor on live `IBoard` / `CellView`. Does **not** hit the network.

```json
{
  "x": 3,
  "y": 4,
  "exists": true,
  "state": "taken" | "free",
  "flagged": false,
  "minesAround": null,
  "effects": [],
  "animator": null | "open" | "explosion" | "flag",
  "active": true
}
```

- `state` from `CellView.State` (`ICellTakenState` / free).
- `flagged` from taken state.
- `minesAround` only when free.
- `effects` from `CellEffects` type names if already enumerable; else empty list — do not invent.
- `animator`: if `CellAnimator` / flag animator `IsPlaying`. Expose a small read API on `CellView` (`Inspect()` or public `IsAnimating` + kind). Do not scrape SpriteRenderer names as the contract.
- Missing cell → `exists: false`.

### `game_wait_visual` (new)

Args: `timeout_ms` default 5000.

Completes when no own-board cell reports animator playing, or timeout (`HasError`). Needed before inspect after open/card.

Implementation: poll CellViews. Do not wait on observation.

---

## Server apply site

`LastManStandingTurnBasedRound.Process` after the health/mana/moves init and after `RestoreCards`, before `RecordGameStarted`:

```
if (_matchOptions.Fixture != null)
    AgentMatchFixtureApplier.Apply(_gameContext, _matchOptions.Fixture, snapshot);
```

Then set `_currentPlayer` according to HumanGoesFirst **instead of always bot**.

Applier is a static class next to the publisher. It must record snapshot diffs (card remove/add, mana, moves, board state if the client needs initial taken/free). If the board is pre-filled, send a board snapshot so the client CellViews match (`RecordBoardStateUpdate` or the existing full-board init path). If the current init snapshot does not push individual cells until first generate, add whatever the client already uses on first generate — **do not leave the Unity board empty while the server has cells**.

Check how the client learns the initial board. First generate today happens on first open and snapshots follow. A pre-filled board that never generates will **desync** unless we snapshot it at init. Lock: after Apply, record a full board state into the init `MoveSnapshot` so `BoardSnapshotHandler` builds the matching taken/free/flags.

Tests (xUnit, no Orleans):

File: `backend/Tools/Tests/Game/AgentMatchFixtureApplierTests.cs`

1. Apply layout `m t` padded to size → cell (0,0) has mine, (1,0) taken clean; `EnsureGenerated` does not move the mine.
2. Layout wider than size → throws.
3. Closed mine is **not** in default `AgentObservationBuilder` `HasMine`.
4. Hand replace: RestoreCards then Apply hand `[Bloodhound]` → exactly one card, type Bloodhound.

File: `backend/Tools/Tests/Game/AgentLegalPlaysBuilderTests.cs`

1. Medic → `needsPosition false`, empty cells.
2. Bloodhound on a 5×5 taken board → cells non-empty; a fully free board (if constructed) → empty cells.
3. Recycler with two cards → extraCardIds contains the other id.

Run:

```bash
dotnet test backend/Tools/Tests/Tests.csproj -- --filter-class "*AgentMatchFixture*" --filter-class "*AgentLegalPlays*"
```

---

## Style

Follow `.agents/docs/CODE_STYLE_FULL.md`, `.agents/docs/API_DESIGN_FULL.md`, `.agents/docs/TESTING.md`, `.agents/docs/GAMEPLAY.md`.

- Backend file-scoped namespaces as neighbors.
- Tests: xUnit v3, FluentAssertions; no full Orleans cluster.
- MCP tools stay thin wrappers over `GameAgentBridge`.

---

## Explicitly forbidden

- Many matches, bot-vs-bot, Instant ActionDelay batch
- Pixel screenshots / golden frames / Game View png as the contract
- Wrapping the whole cheats window
- `EditorApplication.isPlaying = true`
- Changing LMS / TimeLimited timers
- Leaking closed `HasMine` in default observation or legal_plays
- Using `board-solver.py` as legal card targets
- Adding the mode to `MenuPlay`
- Replacing `IPlayer.Board` with a new object
- Changing `EmptyResponse`
- Prefab catalog work (other task)

---

## План реализации

#### 1 Fixture apply + tests
- **Статус:** [x] completed
- **Цель:** `BoardLayoutParser.Apply` + `AgentMatchFixtureApplier` детерминируют доску/руку на существующем игроке; тесты зелёные.
- **Как:** Apply on existing `IBoard`. Applier for hand/mana/moves. Do not wire transport yet — tests construct players like `AgentObservationBuilderTests`.
- **Проверка:** filter-class `*AgentMatchFixture*`. EnsureGenerated no-op after apply. Observation builder still hides closed mines.
- **Файлы:** `BoardLayoutParser.cs`, `shared/Game/Agent/AgentMatchFixture.cs` [новый], `backend/Game/GamePlay/Agent/AgentMatchFixtureApplier.cs` [новый], tests [новые]
- **Зависит от:** —
- **Блокирует:** 2, 3

#### 2 Transport + round hook
- **Статус:** [x] completed
- **Цель:** CreateWithBot с Fixture доезжает до turn-based round; человек ходит первым; клиент получает board snapshot.
- **Как:** Thread DTO through SharedMatchmaking → Meta → MatchPayloads → MatchCreateOptions. Hook round init. Init snapshot includes pre-filled board.
- **Проверка:** Production callers compile with Fixture null. Round file still has no TimerCountdown. Bot-first when fixture is null.
- **Файлы:** `SharedMatchmaking.cs`, `Matchmaking.cs`, `BackendEndpoints.cs`, `MatchmakingCommands.cs`, `MatchFactory.cs`, `MatchPayloads.cs`, `Session.cs` (`MatchCreateOptions`), `LastManStandingTurnBasedRound.cs`, `IMatchmaking` on meta if present
- **Зависит от:** 1
- **Блокирует:** 3

#### 3 MCP start fixture + use_card by type
- **Статус:** [x] completed
- **Цель:** Агент стартует сценарий `hand=Bloodhound` + DSL-доска и играет карту по имени.
- **Как:** Extend `game_start_vs_bot` parameters. Extend `GameAgentBridge.UseCard` / MCP to resolve type → id. Update `game-agent.py`.
- **Проверка:** Invalid type → error. Type not in hand → error. Guid path unchanged.
- **Файлы:** `GameAgentMcpTools.cs`, `GameAgentBridge.cs`, `Matchmaking.cs` (client), `tools/scripts/game-agent.py`
- **Зависит от:** 2
- **Блокирует:** —

#### 4 Legal plays
- **Статус:** [x] completed
- **Цель:** `game_legal_plays` возвращает клетки Bloodhound и extra ids Recycler с сервера.
- **Как:** `SharedAgentLegalPlaysRequest` + command + `AgentLegalPlaysBuilder` + MCP tool + tests.
- **Проверка:** filter-class `*AgentLegalPlays*`. No mine leak. Medic has empty cells.
- **Файлы:** `shared/Game/Agent/*`, `backend/Game/GamePlay/Agent/AgentLegalPlaysBuilder.cs`, GameCommand, `GameAgentBridge`, `GameAgentMcpTools`
- **Зависит от:** 2
- **Блокирует:** —

#### 5 Inspect cell + wait_visual
- **Статус:** [x] completed
- **Цель:** После open/карты агент ждёт анимацию и читает Unity-state клетки, не серверный ASCII.
- **Как:** `CellView` read API. Bridge inspect/wait. Two MCP tools. No screenshots.
- **Проверка:** Missing cell → exists false. wait_visual times out instead of hanging. Does not call the network.
- **Файлы:** `CellView.cs`, `CellAnimator.cs` (expose IsPlaying kind), `GameAgentBridge.cs`, `GameAgentMcpTools.cs`
- **Зависит от:** —
- **Блокирует:** —

---

## Ключевые файлы

| Файл | Роль |
|------|------|
| `backend/Game/GamePlay/Boards/BoardLayoutParser.cs` | DSL; add Apply on existing board |
| `backend/Tools/Tests/Game/BoardParser.cs` | Same alphabet; tests |
| `backend/Game/GamePlay/Board/Extensions/BoardActionExtensions.cs` | EnsureGenerated no-op when cells exist |
| `backend/Game/GamePlay/Context/Rounds/LastManStandingTurnBasedRound.cs` | Init, bot-first, RestoreCards |
| `backend/Game/Session/Root/Session.cs` | MatchCreateOptions |
| `shared/Backend/SharedMatchmaking.cs` | CreateWithBot |
| `backend/Meta/Matches/MatchPayloads.cs` | Pipe DTO, new Id |
| `shared/Game/Agent/CardUsePayloadFactory.cs` | Pattern for exhaustive switches |
| `client/Assets/GamePlay/Agent/GameAgentBridge.cs` | Client API |
| `client/Assets/GamePlay/Editor/Agent/GameAgentMcpTools.cs` | MCP wrappers |
| `client/Assets/GamePlay/Boards/Cells/CellView.cs` | Semantic inspect |
| `tools/scripts/game-agent.py` | CLI |

## Документация к прочтению

- `.agents/docs/GAMEPLAY.md` — round, cards, board generate
- `.agents/docs/TESTING.md` — xUnit, log conversion
- `.agents/docs/API_DESIGN_FULL.md` — lists, UniTask
- `.agents/docs/CODE_STYLE_FULL.md`
- `.agents/docs/COMMON_ORLEANS.md` — GenerateSerializer Ids on RequestWithBot
- `.agents/docs/CARD_EFFECTS.md` — Bloodhound pattern / targeting
- `.agents/docs/CLAUDE_MISTAKES.md`

## Риски

- Pre-filled board without an init snapshot leaves Unity at empty Taken while the server already has mines.
- RestoreCards after fixture hand undoes the hand — Apply **after** RestoreCards, human only.
- `RequestWithBot` new field with a reused Orleans Id will corrupt pipe payloads in mixed-version clusters; use a new `[Id]`.
- Legal plays that call `Use` mutate the match. Builder must be read-only.
- Inspect during animation reads the previous sprite/state. `game_wait_visual` is part of the contract, not optional fluff.
- Fixture size vs `BoardOptions.Size` / scene 16×16: never shrink the client grid.
