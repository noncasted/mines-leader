## Card Cell Visualization

### Что сделано
- Добавлен `IReadOnlyList<Position>? TargetCells` в `ICardActionData` (default member => null); 16 карт, меняющих поле, заполняют его позициями выбранных клеток.
- Новый клиентский компонент `CellVisuals` с анимациями `PlayCellTarget` (фаза "клетка выбрана") и `PlayCellAction` (фаза "клетка изменена").
- `CardActionSnapshotHandler` проигрывает target-анимации перед применением card action. `CellView.EnsureFree/EnsureTaken` автоматически зовёт `PlayCellAction`, если клетка была помечена как target.
- Убран `Lock/Unlock` из `ZipZap.cs` — больше не нужен, board events идут через обычный flow.
- `CellOpenAction` (ручное открытие) не затронут — при ручном открытии клетки не помечены как target, анимация не играет.

### Ключевые файлы
- `shared/Game/Snapshots/CardActionSnapshotRecord.cs` — `TargetCells` в 16 snapshot-классах
- `client/Assets/GamePlay/Boards/Cells/CellVisuals.cs` — новый компонент
- `client/Assets/GamePlay/Boards/Cells/CellView.cs` — интеграция с `CellVisuals` в EnsureFree/EnsureTaken
- `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs` — PlayTargetAnimation перед card.Use()

### Заметки
- Архитектура: вместо отдельного BoardTargetSnapshot — default interface member на `ICardActionData`. Target → action порядок гарантируется очерёдностью хендлеров в `SnapshotReceiver` (CardAction раньше Board).
- 16 карт с TargetCells: **Scout** (ZipZap, Bloodhound, ErosionDozer, MinefieldScout, Excavator, ChaosDiamond, ChaosScout, FortuneCookie); **CrossBoard** (Trebuchet, OpponentBomb, ChainReaction, MineCluster, CarpetBomb, FortuneBlast). Buff/hand/resource карты — без TargetCells.
- Анимационные ассеты (`ForwardAnimationAsset` для `_cellTargetData`/`_cellActionData`) и привязка `CellVisuals` к префабу `CellView` — ручной шаг в Unity Editor.
