## Полный переход на мануальные снапшоты

### Что сделано

- `MoveSnapshot` — чистый продьюсер `Record*`-вызовов; автоподписка `HandleBoards/HandlePlayers` удалена. `Record*` встроен в сеттеры `IMana/IHealth/IMoves/IModifiers` — мутировать ресурс без снапшота невозможно.
- 42 карты, 15 команд, раунды, бот, `ModifierDisposeAction` мигрированы на явные `Record*`. Введён `CardUseContext { Invoker, Snapshot, CardId }`, `MoveSnapshotAccessor` удалён.
- Board lifecycle явный: `IBoard.Updated/OnUpdated` удалены; `Revealer.Reveal(positions)` возвращает все открытые клетки; `Scanner.Recalculate(snapshot)` сам пишет `MinesAround` records.
- Ordering в снапшоте через `BeginInsertAt(prefixMark)`: `Mana → Moves → [card records incl. RecordCardUse] → CardRemove → CardAdd(stash)`. Клиентская choreography: spend → animation → leave hand → land in stash.
- `OpenedCells: IReadOnlyList<OpenedCell>` с `Position + MinesAround` — reveal-карты больше не пишут `RecordMines`, клиент открывает клетки синхронно с `RecordCardUse`.
- Diff-guard (`SnapshotApplier` + `SnapshotDiffGuard`) валидирует каждую команду/round block; toggle через `IClusterFlags.SnapshotDiffGuardEnabled` (Console → Features). В тестах включён.

### Ключевые файлы

- `backend/Game/GamePlay/Context/MoveSnapshot.cs` — центр, `Record*` API, `BeginInsertAt`.
- `backend/Game/GamePlay/Cards/ICard.cs` — `CardUseContext`.
- `backend/Game/GamePlay/Snapshots/SnapshotDiffGuard.cs` + `SnapshotApplier.cs` + `GameStateCapture.cs`.
- `backend/Game/GamePlay/Commands/CardUseCommand.cs` — ordering.
- `shared/Game/Snapshots/CardActionSnapshotRecord.cs` — `OpenedCell` struct.
- `shared/Game/Snapshots/PlayerSnapshotRecord.cs` — `ModifierUpdate`, `CardAdd.IsStash`, round records.
- `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs` — playback.
- `client/Assets/GamePlay/Loop/PvP/PvPScopeExtensions.cs` — условная регистрация round-handler'ов.
- `backend/Game/Global/SessionFactory.cs` — `IClusterFlags` в pass-dependencies.

### Заметки

- **Snapshot-in-setter** — главная архитектурная находка: `Mana.Use(snapshot, cost)` вместо `Mana.Use(cost) + snapshot.RecordMana(...)`. Compile-time гарантия, что мутация всегда запишется.
- **Ordering-bug**: `CardRemove` нельзя писать до `RecordCardUse` — клиент синхронно удаляет карту из `Hand` и `CardActionSnapshotHandler.First(t => t.Id == record.CardId)` падает.
- **DI scope для diff-guard**: `SnapshotDiffGuard` зависит от `IClusterFlags`; passed в каждый session scope в `SessionFactory.PassDefaultDependencies`.
- **Round-handlers условны**: `TimeLimited` / `LastManStanding` round registered только в соответствующем switch case в `PvPScopeExtensions`. Handler регистрируется рядом.
- Tests: 581 passed / 0 failed / 2 skipped (baseline до рефактора был 359/5).
- Pre-existing issue вне scope задачи: `CellMultipleOpenAction` резолвит `IBoardActions` из game-scope (регистрация в player-entity).
