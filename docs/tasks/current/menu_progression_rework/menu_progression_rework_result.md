## menu_progression_rework — Результат

### Статус: Завершено

### Что сделано
1. В `client/Assets/Meta/Progression` создана доменная модель: `IProgression`, `IProgressionMilestone`, `ProgressionMilestone`, `ProgressionMilestoneStatus`, `LootBoxOffer`.
2. Реализован сервис `ProgressionService`, который подписывается на `ProgressionProjection`, `LootProjection` и `LootProgressionOptions`, формирует реактивный список майлстоунов и отдаёт `IProgression`.
3. Зарегистрирован `ProgressionService` в DI (`MetaServicesExtensions.cs`) как `IProgression` + `IScopeSetup`.
4. `MenuProgression` переписан как чистый view: слушает `IProgression.CurrentProgress` и `IProgression.Milestones`, создаёт/удаляет UI-элементы, обновляет бар, открывает лутбоксы через `IProgression`.
5. `ProgressionMilestone.cs` (UI) переписан: слушает `Status`/`BoxId`, переключает спрайты `_taken`/`_active`/`_locked`, блокирует кнопку при пустом `BoxId`.
6. Убраны прямые зависимости `MenuProgression` от `IBackendProjection`, `ILootProgressionConfigs`, `IMetaBackend`, `ICardsRegistry`.
7. Проверена сборка: `dotnet build client/Meta.csproj` и `dotnet build client/Menu.csproj` проходят без ошибок.

### Измененные файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|
| `client/Assets/Meta/Progression/ProgressionMilestoneStatus.cs` | Создан enum `ProgressionMilestoneStatus` | [1] | `dotnet build client/Meta.csproj` |
| `client/Assets/Meta/Progression/ProgressionMilestone.cs` | Создана модель `ProgressionMilestone` с `Status`/`BoxId` | [1] | `dotnet build client/Meta.csproj` |
| `client/Assets/Meta/Progression/IProgression.cs` | Создан интерфейс `IProgression` и `LootBoxOffer` | [1] | `dotnet build client/Meta.csproj` |
| `client/Assets/Meta/Progression/Progression.cs` | Сервис `ProgressionService`, собирающий модель из бэкенд-проекций | [2] | `dotnet build client/Meta.csproj` |
| `client/Assets/Meta/MetaServicesExtensions.cs` | Регистрация `ProgressionService` в DI | [2] | `dotnet build client/Meta.csproj` |
| `client/Meta.csproj` | Добавлены `Compile Include` для новых файлов | [1], [2] | `dotnet build client/Meta.csproj` |
| `client/Assets/Menu/Screens/Progression/ProgressionMilestone.cs` | Переключение спрайтов по статусу, подписки на `Status`/`BoxId` | [3] | `dotnet build client/Menu.csproj` |
| `client/Assets/Menu/Screens/Progression/MenuProgression.cs` | Только view: слушает `IProgression`, управляет UI, делегирует открытие лутбокса | [4] | `dotnet build client/Menu.csproj`; `grep` — нет `IBackendProjection`/`ILootProgressionConfigs`/`IMetaBackend` |

### Отличия от плана
1. В `IProgression` вместо прямого `IReadOnlyList<ProgressionMilestone>` используется `IViewableList<IProgressionMilestone>` — он реализует `IReadOnlyList` и уведомляет UI о смене конфига (добавление/удаление майлстоунов). Это необходимо, чтобы экран не потерял обновление списка при получении нового `LootProgressionOptions`.
2. В модель `IProgressionMilestone` добавлено `IViewableProperty<Guid> BoxId` — требуется для корректной блокировки кнопки и передачи корректного идентификатора лутбокса в `IProgression.OpenLootBox`. Исходное описание содержало только `Required` и `Status`.
3. Класс сервиса назван `ProgressionService`, а не `Progression` (в файле `Progression.cs`), чтобы избежать конфликта имени класса с namespace `Meta.Progression` в `MetaServicesExtensions.cs`.
4. UI-элемент `ProgressionMilestone` не переименовывался в `ProgressionMilestoneView` — конфликт имён решён за счёт нахождения UI-класса в `Menu.Screens`, а модели в `Meta.Progression`, и использования интерфейса `IProgressionMilestone` в UI. Префаб не требовал обновления.
5. Открытие лутбокса возвращает `LootBoxOffer` с уже разрешёнными `ICardDefinition` — `MenuProgression` больше не инжектирует `ICardsRegistry`.

### Нерешенные вопросы
- Нет. Сборка проходит, требования покрыты. Ручное тестирование в Unity не проводилось — требуется smoke-test в редакторе для проверки визуального поведения спрайтов и открытия лутбоксов.
