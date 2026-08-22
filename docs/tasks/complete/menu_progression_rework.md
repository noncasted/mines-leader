## menu_progression_rework

### Что сделано
- Данные прогрессии вынесены из `MenuProgression` в `Meta.Progression`: `IProgression`, `ProgressionMilestone`, `ProgressionService`.
- Сервис слушает `ProgressionProjection` / `LootProjection` / `LootProgressionOptions` и отдаёт реактивный список майлстоунов.
- UI — чистый view: спрайты `_taken` / `_active` / `_locked` по `Status`, открытие лутбокса через `IProgression`.

### Ключевые файлы
- `client/Assets/Meta/Progression/Progression.cs` — `IProgression` + `ProgressionService`
- `client/Assets/Menu/Screens/Progression/MenuProgression.cs`
- `client/Assets/Menu/Screens/Progression/ProgressionMilestone.cs`

### Заметки
- Класс сервиса назван `ProgressionService`, чтобы не конфликтовать с namespace `Meta.Progression`.
- В майлстоун добавлен `BoxId` — без него нельзя открыть лутбокс и заблокировать кнопку.
