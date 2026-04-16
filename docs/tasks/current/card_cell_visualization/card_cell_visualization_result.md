## Card Cell Visualization — Результат

### Статус: Завершено

### Что сделано
1. Добавлен `IReadOnlyList<Position>? TargetCells` в интерфейс `ICardActionData` (default => null)
2. 16 карт, меняющих поле, заполняют `TargetCells` позициями клеток-целей
3. Убран Lock/Unlock из ZipZap — board events теперь записываются нормально
4. Создан компонент `CellVisuals` с анимациями `PlayCellTarget` и `PlayCellAction`
5. `CellView` интегрирован с `CellVisuals` — PlayCellAction вызывается автоматически при EnsureFree/EnsureTaken если клетка была помечена как target
6. `CardActionSnapshotHandler` играет target-анимации перед обработкой card action
7. Ручное открытие клеток (CellOpenAction) не затронуто

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `shared/Game/Snapshots/CardActionSnapshotRecord.cs` | Добавлен TargetCells в ICardActionData и 16 snapshot-классов |
| `backend/Game/GamePlay/Cards/Scout/ZipZap.cs` | Убран Lock/Unlock, убран snapshotAccessor, Targets -> TargetCells |
| `backend/Game/GamePlay/Cards/Scout/Bloodhound.cs` | Заполняет TargetCells |
| `backend/Game/GamePlay/Cards/Scout/ErosionDozer.cs` | Заполняет TargetCells |
| `backend/Game/GamePlay/Cards/Scout/MinefieldScout.cs` | Заполняет TargetCells |
| `backend/Game/GamePlay/Cards/Scout/Excavator.cs` | Заполняет TargetCells |
| `backend/Game/GamePlay/Cards/Scout/ChaosDiamond.cs` | Заполняет TargetCells |
| `backend/Game/GamePlay/Cards/Scout/ChaosScout.cs` | Заполняет TargetCells |
| `backend/Game/GamePlay/Cards/Scout/FortuneCookie.cs` | Заполняет TargetCells |
| `backend/Game/GamePlay/Cards/CrossBoard/Trebuchet.cs` | Заполняет TargetCells |
| `backend/Game/GamePlay/Cards/CrossBoard/OpponentBomb.cs` | Заполняет TargetCells |
| `backend/Game/GamePlay/Cards/CrossBoard/ChainReaction.cs` | Заполняет TargetCells |
| `backend/Game/GamePlay/Cards/CrossBoard/MineCluster.cs` | Заполняет TargetCells |
| `backend/Game/GamePlay/Cards/CrossBoard/CarpetBomb.cs` | Заполняет TargetCells |
| `backend/Game/GamePlay/Cards/CrossBoard/FortuneBlast.cs` | Заполняет TargetCells |
| `backend/Tools/Tests/Game/ZipZapTests.cs` | Targets -> TargetCells |
| `client/Assets/GamePlay/Boards/Cells/CellVisuals.cs` | Новый файл — анимации target/action |
| `client/Assets/GamePlay/Boards/Cells/CellView.cs` | Интеграция CellVisuals, PlayCellAction в EnsureFree/EnsureTaken |
| `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs` | PlayTargetAnimation перед card.Use() |
| `client/Assets/GamePlay/Cards/Entities/Actions/ZipZap/CardZipZapAction.cs` | Targets -> TargetCells |
| `client/GamePlay.csproj` | Добавлен CellVisuals.cs |

### Нерешенные вопросы
1. **Анимационные ассеты** — нужно создать ForwardAnimationAsset для _cellTargetData и _cellActionData в Unity Editor
2. **Префаб CellView** — нужно добавить CellVisuals компонент на префаб в Unity Editor и привязать SerializeField
3. **Длительность анимаций** — PlayCellTarget ждет окончания анимации перед продолжением, настройка через ForwardAnimationAsset.Time
