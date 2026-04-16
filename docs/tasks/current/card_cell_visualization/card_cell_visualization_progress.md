## Card Cell Visualization — Рабочие заметки

### Статус: В работе

### Заметки

### Архитектурное решение
Вместо создания нового типа снапшота и ручного управления — используем существующую архитектуру:
- TargetCells добавлен как default interface member в ICardActionData (=> null)
- Карты заполняют TargetCells позициями клеток-целей
- На клиенте CardActionSnapshotHandler (обрабатывается первым в очереди) играет PlayCellTarget
- BoardSnapshotHandler (следующий в очереди) применяет board changes
- CellView.EnsureFree/EnsureTaken проверяет HasPendingTarget и играет PlayCellAction
- CellOpenAction не затронут — при ручном открытии клетки не помечены как target

### Lock/Unlock в ZipZap
Убран Lock/Unlock из ZipZap. Теперь ToFree() события записываются в снапшот нормально.
Это корректно — клиент получает CellFree записи для всех затронутых клеток (и прямых, и от Revealer).

### Карты с TargetCells (16 карт)
**Scout (свое поле):** ZipZap, Bloodhound, ErosionDozer, MinefieldScout, Excavator, ChaosDiamond, ChaosScout, FortuneCookie
**CrossBoard (поле оппонента):** Trebuchet, OpponentBomb, ChainReaction, MineCluster, CarpetBomb, FortuneBlast
**Без TargetCells (не меняют клетки):** TrebuchetAimer, Smoke, Medic, Siphon, все buff/hand/resource карты

### Gravedigger
Snapshot тип существует, но реализации карты нет (удалена). TargetCells добавлен в snapshot класс на всякий случай.
