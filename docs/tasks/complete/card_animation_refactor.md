## Card Animation Refactor

### Что сделано
- Из `ICardActionData` убраны optional `TargetCells`/`OpenedCells`/`UpdatedFreeCells` — остался только `TargetPlayer`.
- Анимации клеток переехали в `ICardActionSync` через `IBoardCellsAnimator` (`BoardCellsAnimator` / `MenuBoardCellsAnimator`).
- `CardActionSnapshotHandler` вызывает только `card.Use()`; menu preview — только `_syncRegistry.Dispatch()`.
- Snapshot-поля синхронизированы с backend: nullable только там, где поле реально может отсутствовать.

### Ключевые файлы
- `shared/Game/Snapshots/CardActionSnapshotRecord.cs`
- `client/Assets/GamePlay/Boards/Cells/IBoardCellsAnimator.cs`
- `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs`
- `client/Assets/GamePlay/Cards/Entities/Actions/*/*.cs`

### Заметки
- `Gravedigger` / `TrebuchetAimer` / `Purge` больше не имеют `TargetCells`.
- Карты с `_Max` должны выбирать config по `payload.Type`. `Blackout` исправлен; `Smoke` / `Frost` / `FogOfWar` всё ещё хардкодят `_Normal`.
- Не регистрировать no-op `ICardRandomAnimator` в меню — на сцене `Menu_Board` уже есть настоящий `CardRandomAnimator`.
