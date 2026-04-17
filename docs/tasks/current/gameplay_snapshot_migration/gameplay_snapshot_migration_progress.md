## Gameplay Snapshot Migration — Рабочие заметки

### Статус: В работе

### Заметки
<!-- Сюда записываются находки, решения и полезная информация по ходу реализации -->

### [init] Исследование инфраструктуры снапшотов
- `SharedMoveSnapshot` union уже содержит 9 членов (теги 0–8). Новые records добавляются с тегами 9+.
- `MoveSnapshot` на бэке — билдер, у него есть специальный `BeginInsertAt` для вклейки в середину (важно при расширении).
- `SnapshotApplier` — симметричный applier для diff-guard; для `GameStartedRecord` пустая ветка `case` — прецедент для событий без state.
- Клиентский `SnapshotReceiver` — `OneWayCommand<SharedMoveSnapshot>`, диспатч по типу record через словарь.
- `BoardSnapshotHandler` обрабатывает `SharedBoardSnapshot.Records` — клеточные события. Поле `BoardState` (Mines/Flags) не покрыто ни одним record и в клиенте Board.cs не читается.

### [init] Текущие точки `NetworkProperty` в геймплее (8)
- `Board.cs` — `BoardState`, получает через `[Inject]`, `State` expose-нут, но использование снаружи надо проверить.
- `Hand.cs` — `PlayerHandState` принят в ctor, но не читается (мёртвый код).
- `Deck.cs`, `Stash.cs` — читают `_state.Value.Queue.Count`/`_state.Value.Count` в `Advise`.
- `PlayerModifiers.cs` — копирует словарь значений в `ViewableDictionary` в `Advise`.
- `GameState.cs` — `Winner != Guid.Empty` → завершает матч.
- `TimeLimitedGameRound.cs` — `CurrentPlayer` + `SecondsLeft[playerId]`.
- `LastManStandingRound.cs` — `CurrentPlayer` + `SecondsLeft`.

### [init] Backend-источники state
- `GameFlow.Process()` — `_state.Update(state => state.Winner = winner)` в самом конце.
- `TimeLimitedRound` — `_state.Update(...)` в 4 местах (init SecondsLeft, CurrentPlayer, timer tick, AddTimeForAction).
- `LastManStandingRound` — 3 места (CurrentPlayer, CurrentRound++, timer tick).

### [init] Регистрации на клиенте
- `GamePlayerFactory.Build` — 5× `RegisterProperty<TState>` (Board/Modifiers/Hand/Stash/Deck).
- `PvPScopeExtensions.Construct` — 3× `.WithProperty<TState>` через `AddNetworkService` (GameFlow/TimeLimitedRound/LastManStandingRound).

### [phase1] Shared records
Добавлено в `shared/Game/Snapshots/`:
- `PlayerSnapshotRecord.DeckUpdate`, `StashUpdate`, `BoardStateUpdate`
- Новый `GameFlowRecords.cs`: `GameCompletedRecord`, `TimeLimitedRoundRecord`, `LastManStandingRoundRecord`
- Union `IMoveSnapshotRecord` расширен тегами 9–14.

### [phase2] Backend мигрирован
Все `ValueProperty<TState>` удалены из:
- `GameFlow` → эмитит `RecordGameCompleted` через `ISnapshotSender` в конце `Process()`.
- `TimeLimitedRound` — локальный `Dictionary<Guid,long> _secondsLeft`, `_currentPlayerId`; эмит `RecordTimeLimitedRound` на каждое изменение (init, round start, timer tick, action bonus).
- `LastManStandingRound` — локальные `_secondsLeft/_currentRound/_currentPlayerId`; `RecordLastManStandingRound` аналогично.
- `Board` — просто `Guid ownerId + BoardOptions`; `BoardMinesScanner` хранит `Mines/Flags` локально, принимает опциональный `MoveSnapshot` в `Recalculate(...)` и сам эмитит `RecordBoardStateUpdate` при изменении.
- `Hand/Deck/Stash` — локальные списки, state-property убраны. Обновления клиенту через `RecordDeckUpdate/RecordStashUpdate` в местах вызова.
- `Modifiers` — без state; `ModifierUpdate` уже эмитится в `Set`.

Добавлено 5 методов в `MoveSnapshot`: `RecordDeckUpdate`, `RecordStashUpdate`, `RecordBoardStateUpdate`, `RecordGameCompleted`, `RecordTimeLimitedRound`, `RecordLastManStandingRound`.

`SnapshotApplier` получил пустые `case`-ветки для 6 новых типов (не влияют на diff-guard capture).

Обновлены места эмиссии deck/stash (CardUseCommand, Recycler, Salvage, Scavenger, MysticDraw, GraveDigger, HandScramble, GamblersRuin, SabotageDeck, BotCardAction).

`MoveSnapshotBoardExtensions.RecordReveal` теперь эмитит `RecordBoardStateUpdate` после флуд-филла. Все вызовы `MinesScanner.Recalculate()` заменены на `Recalculate(snapshot)`.

Сборка `Game.csproj` и `Tests.csproj` — 0 ошибок.
