## Отображение текущих модификаторов игрока — Результат

### Статус: Завершено

### Что сделано
1. **Shared-контракты**: созданы `IModifierOverview`, `DurationalModifierOverview` (с `SourceId`, `Equals`/`GetHashCode`). Обновлены `PlayerModifiersState`, `PlayerSnapshotRecord.ModifierUpdate`, `GameStateSnapshot`, `MoveSnapshot`, `SnapshotApplier`.
2. **Backend — ядро модификаторов**: `Modifiers` теперь хранит `Dictionary<Guid, IModifierSource>` вместо `Dictionary<PlayerModifier, float>`. Публичное API `Values` возвращает вычисленный словарь. Добавлены методы `Add`, `Update`, `Remove`, `RemoveOne`, `Reset`.
3. **Backend — раундовые экшены**: `IRoundAction.Tick()` возвращает `bool`. `RoundActionService` вызывает `Tick()` каждый раунд. Создан `ModifierRoundAction` (замена `ModifierDisposeAction`). Все cell-эффект `*DisposeAction` обновлены.
4. **Backend — карты и команды**: все Buff, Resource, CrossBoard карты обновлены для работы с `DurationModifierSource` + `ModifierRoundAction`. `OpenCellCommand` использует `RemoveOne` для Shield.
5. **Клиент — синхронизация**: `PlayerModifiers` теперь имеет `ViewableList<DurationalModifierOverview> Overviews`. `PlayerModifierSnapshotHandler` обрабатывает overview-записи.
6. **Клиент — UI оверлея**: созданы `ModifierDescriptionsConfig` (ScriptableObject), `PlayerModifierEntryView` (иконка), `PlayerModifierTooltipView` (тултип), `PlayerModifiersView` (контейнер), `ModifierEntryPrefabDefinition`.
7. **Тесты**: все 616 backend-тестов проходят.

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `shared/Game/Player/IModifierOverview.cs` | Новый интерфейс |
| `shared/Game/Player/DurationalModifierOverview.cs` | Новый класс с SourceId и equality |
| `shared/Game/Player/PlayerModifiersState.cs` | List<DurationalModifierOverview> вместо Dictionary |
| `shared/Game/Snapshots/PlayerSnapshotRecord.cs` | ModifierUpdate теперь содержит Overview |
| `backend/Game/GamePlay/Context/MoveSnapshot.cs` | RecordModifierUpdate принимает Overview |
| `backend/Game/GamePlay/Players/IModifierSource.cs` | Новый интерфейс |
| `backend/Game/GamePlay/Players/DurationModifierSource.cs` | Новая реализация с Tick() |
| `backend/Game/GamePlay/Players/Modifiers.cs` | Полная переработка на источники |
| `backend/Game/GamePlay/Context/RoundActionService.cs` | Tick() вызывается каждый раунд |
| `backend/Game/GamePlay/Context/ModifierRoundAction.cs` | Новый раундовый экшен |
| `backend/Game/GamePlay/Snapshots/GameStateSnapshot.cs` | Modifiers теперь List<Overview> |
| `backend/Game/GamePlay/Snapshots/GameStateCapture.cs` | Захват overview из Sources |
| `backend/Game/GamePlay/Snapshots/SnapshotApplier.cs` | Обновление списка overview |
| `backend/Game/GamePlay/Snapshots/SnapshotDiffGuard.cs` | Сравнение overview-списков |
| `backend/Game/GamePlay/Commands/OpenCellCommand.cs` | RemoveOne для Shield |
| `backend/Game/GamePlay/Cards/**/*` | Все карты обновлены |
| `client/Assets/GamePlay/Players/Entity/Modifiers/PlayerModifiers.cs` | ViewableList overviews |
| `client/Assets/GamePlay/Sync/PlayerModifierSnapshotHandler.cs` | Обработка overview |
| `client/Assets/GamePlay/UI/Overlay/*.cs` | Новые UI-компоненты |
| `client/GamePlay.csproj` | Добавлены новые файлы |
| `backend/Tools/Tests/Game/*Tests.cs` | Все тесты обновлены |

### Отличия от плана
- `IsRemoved` не добавлен в `DurationalModifierOverview`. Вместо этого используется конвенция `TurnsToEnd == 0` для сигнала удаления.
- `PlayerModifiersView` использует `[SerializeField]` для префаба вместо `Prefabs.ModifierEntry` (требуется запуск PrefabBuilder).
- `RoundActionService.Schedule` потерял параметр `int rounds` — теперь все экшены отслеживают длительность самостоятельно.

### Тестовое покрытие

Все **633** backend-тестов проходят (616 обновленных + **17 новых**).

| Класс | Новых тестов | Что проверяется |
|-------|-------------|----------------|
| `DurationModifierSourceTests` | 5 | Tick уменьшает TurnsToEnd, возвращает true при 0, не трогает -1, GetOverview корректен |
| `ModifierRoundActionTests` | 3 | При живом сурсе — Update, при истечении — Remove |
| `ModifiersTests` | 4 | Суммирование нескольких источников, RemoveOne удаляет один, RemoveOne при пустом — no-op |
| `SnapshotApplierModifierTests` | 3 | Добавление, обновление, удаление overview в GameStateSnapshot |
| `OpenCellCommandTests` | 2 | RemoveOne вызывается при щите, TakeDamage при отсутствии щита |
