---
task: menu_progression_rework
status: completed
phase: complete
updated: 2026-07-05
total_steps: 5
completed_steps: [1, 2, 3, 4, 5]
blocked_steps: []
---

## Задача: Реворк экрана прогрессии с выносом данных в Meta

### Что я хочу

Нужно переделать экран прогрессии (`client/Assets/Menu/Screens/Progression/MenuProgression.cs`) так, чтобы он перестал заниматься обработкой конфигов, бэкенд-прогрессий и лута. Вся работа с данными должна переехать в `client/Assets/Meta/Progression`. Там должен появиться сервис, который получает конфиг лута (`ILootProgressionConfigs`) и сами прогрессионные проекции (`ProgressionProjection`, `LootProjection`), а наружу отдаёт реактивную модель `IProgression`.

Каждый майлстоун представлен отдельным объектом с `IViewableProperty<ProgressionMilestoneStatus>`, за которым UI слушает. Статусы: `Unlocked`, `Active`, `Locked`. UI-объект `ProgressionMilestone.cs` должен переключать спрайт хендла в зависимости от статуса: `_taken`, `_active`, `_locked`.

`MenuProgression` после реворка занимается только view: спавнит/удаляет элементы, слушает прогресс и статусы, обновляет бар, делегирует открытие лутбокса в слой данных.

### Цель

1. Вынести работу с данными прогрессии из `MenuProgression.cs` в `client/Assets/Meta/Progression`.
2. Создать `IProgression` с коллекцией майлстоунов и реактивным текущим прогрессом.
3. Сделать модель `ProgressionMilestone` с `int Required` и `IViewableProperty<ProgressionMilestoneStatus> Status`.
4. Определить `ProgressionMilestoneStatus` как `Unlocked`, `Active`, `Locked`.
5. Обеспечить, чтобы каждый майлстоун был отдельным слушаемым объектом.
6. В `Progression.cs` обрабатывать конфиг лута, обновления прогрессии и лута.
7. Оставить в `MenuProgression.cs` только view-логику.
8. Переделать `ProgressionMilestone.cs`: в зависимости от `Status` подставлять `_taken`, `_active` или `_locked`.
9. Зарегистрировать новый сервис в DI и добавить файлы в `.csproj`.
10. Проверить, что проект собирается.

### Контекст

Сейчас `MenuProgression` сам слушает `IBackendProjection<ProgressionProjection>`, `IBackendProjection<LootProjection>` и `ILootProgressionConfigs`, сам вычисляет статусы майлстоунов и сам открывает лутбоксы через `IMetaBackend`. Это нарушает разделение ответственности: MonoBehaviour-экран не должен знать о бэкенд-проекциях и конфигах. Перенос логики в `Meta/Progression` сделает экран чистым view и позволит переиспользовать прогрессионную модель в других местах (например, в социальных фичах или HUD).

## План реализации

#### [1] Создать доменную модель и интерфейс `IProgression` в `Meta/Progression`
- **Статус:** [x] completed
- **Цель:** После шага в `Meta/Progression` существуют `IProgression`, `ProgressionMilestone` и `ProgressionMilestoneStatus`, а `Meta.csproj` знает о новых файлах.
- **Как:**
  - Создать `client/Assets/Meta/Progression/ProgressionMilestoneStatus.cs` с enum `{ Unlocked, Active, Locked }`.
  - Создать `client/Assets/Meta/Progression/ProgressionMilestone.cs` с `int Required` и `IViewableProperty<ProgressionMilestoneStatus> Status`.
  - Создать `client/Assets/Meta/Progression/IProgression.cs` с `IViewableList<IProgressionMilestone> Milestones` (реактивная коллекция, реализует `IReadOnlyList`) и `IViewableProperty<int> CurrentProgress`.
  - Добавить три `<Compile Include="...">` в `client/Meta.csproj`.
- **Проверка:** `dotnet build client/Meta.csproj` проходит без ошибок.
- **Файлы:**
  - `[новый файл — добавить в Meta.csproj]` `client/Assets/Meta/Progression/ProgressionMilestoneStatus.cs`
  - `[новый файл — добавить в Meta.csproj]` `client/Assets/Meta/Progression/ProgressionMilestone.cs`
  - `[новый файл — добавить в Meta.csproj]` `client/Assets/Meta/Progression/IProgression.cs`
  - `client/Meta.csproj`
- **Зависит от:** —
- **Блокирует:** [2]

#### [2] Реализовать сервис `Progression` и зарегистрировать его в DI
- **Статус:** [x] completed
- **Как:**
  - Создать `client/Assets/Meta/Progression/Progression.cs`, реализующий `IProgression`, `IScopeSetup` (класс сервиса назван `ProgressionService` во избежание конфликта с namespace `Meta.Progression`).
  - В конструкторе инжектировать `IBackendProjection<ProgressionProjection>`, `IBackendProjection<LootProjection>`, `ILootProgressionConfigs`, `IMetaBackend`, `ICardsRegistry`.
  - В `OnSetup` подписаться на конфиг и проекции; при обновлении пересоздавать `ViewableList<ProgressionMilestone>` и обновлять `CurrentProgress`/`Status`/`BoxId` каждого майлстоуна.
  - Добавить `OpenLootBox(IProgressionMilestone milestone)` и `ChooseLootReward(Guid boxId, CardType card)`.
  - Добавить `<Compile Include>` в `client/Meta.csproj`.
  - Зарегистрировать `builder.Register<ProgressionService>().As<IProgression>().As<IScopeSetup>()` в `client/Assets/Meta/MetaServicesExtensions.cs`.
- **Проверка:** `dotnet build client/Meta.csproj` проходит; `IProgression` разрешается через DI.
- **Файлы:**
  - `[новый файл — добавить в Meta.csproj]` `client/Assets/Meta/Progression/Progression.cs`
  - `client/Assets/Meta/MetaServicesExtensions.cs`
  - `client/Meta.csproj`
- **Зависит от:** [1]
- **Блокирует:** [3], [4]

#### [3] Переделать UI-элемент `ProgressionMilestone.cs` под спрайты статусов
- **Статус:** [x] completed
- **Цель:** UI-элемент отображает `_taken`, `_active` или `_locked` в зависимости от `ProgressionMilestoneStatus` и предоставляет кнопку/идентификатор лутбокса для `MenuProgression`.
- **Как:**
  - Оставить класс и файл `ProgressionMilestone.cs` в `Menu.Screens` (конфликт имён с `Meta.Progression.ProgressionMilestone` решается использованием интерфейса `IProgressionMilestone` в UI).
  - Убрать сломанную цветовую логику (поля `_lockedColor` и т.д. отсутствуют в SerializeField) и вместо этого менять `_icon.sprite` по `_taken`, `_active`, `_locked`.
  - Добавить публичный метод `Setup(Meta.Progression.IProgressionMilestone milestone, IReadOnlyLifetime lifetime, float normalizedPosition)` и подписываться на `milestone.Status` и `milestone.BoxId` внутри переданного lifetime.
- **Проверка:** `dotnet build client/Menu.csproj` проходит; префаб не изменяется (скрипт остаётся `Menu.Screens.ProgressionMilestone`).
- **Файлы:**
  - `client/Assets/Menu/Screens/Progression/ProgressionMilestone.cs`
- **Зависит от:** [1]
- **Блокирует:** [4]

#### [4] Реворк `MenuProgression.cs` — только view
- **Статус:** [x] completed
- **Цель:** `MenuProgression` больше не зависит от бэкенд-проекций/конфигов; он слушает `IProgression` и обновляет UI.
- **Как:**
  - Убрать инжекцию `IBackendProjection<ProgressionProjection>`, `IBackendProjection<LootProjection>`, `ILootProgressionConfigs`, `IMetaBackend`, `ICardsRegistry`.
  - Инжектировать `IProgression`.
  - В `OnSetup`/`OnEntered` подписаться на `IProgression.CurrentProgress` и `IProgression.Milestones` (ViewableList), обновлять шкалу и список элементов.
  - При появлении нового элемента из `Milestones` создавать `ProgressionMilestone` (UI-элемент), передавать `milestone`, lifetime и normalized position, подписываться на кнопку.
  - При клике по кнопке с доступным лутбоксом вызывать `IProgression.OpenLootBox(...)` и показывать `LootBoxChoicePanel` (если нужно — UI остаётся в `MenuProgression`).
- **Проверка:** `dotnet build client/Menu.csproj` проходит; логика открытия лутбокса сохранена, но вызывается через `IProgression`.
- **Файлы:**
  - `client/Assets/Menu/Screens/Progression/MenuProgression.cs`
- **Зависит от:** [2], [3]
- **Блокирует:** [5]

#### [5] Проверка сборки и базовых сценариев
- **Статус:** [x] completed
- **Цель:** Изменения не ломают компиляцию и сохраняют поведение экрана.
- **Как:**
  - Запустить `dotnet build client/Meta.csproj` и `dotnet build client/Menu.csproj`.
  - Проверить, что `MenuProgression` не содержит прямых обращений к `IBackendProjection<ProgressionProjection>` / `LootProjection` / `ILootProgressionConfigs` (grep).
- **Проверка:** Оба `.csproj` собираются без ошибок; остаточные зависимости отсутствуют.
- **Файлы:** —
- **Зависит от:** [4]
- **Блокирует:** —

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `client/Assets/Menu/Screens/Progression/MenuProgression.cs` | Главный экран — остаётся только view-логика. |
| `client/Assets/Menu/Screens/Progression/ProgressionMilestone.cs` | UI-элемент майлстоуна — переключает спрайты статусов. |
| `client/Assets/Menu/Screens/Progression/ProgressionMilestone.prefab` | Префаб элемента — без изменений, скрипт `Menu.Screens.ProgressionMilestone` остался прежним. |
| `client/Assets/Menu/Screens/Progression/LootBoxChoicePanel.cs` | Панель выбора карты — остаётся без изменений, вызывается из `MenuProgression`. |
| `client/Assets/Meta/Progression/IProgression.cs` | Интерфейс публичной модели прогрессии. |
| `client/Assets/Meta/Progression/ProgressionMilestone.cs` | Модель данных майлстоуна со статусом. |
| `client/Assets/Meta/Progression/ProgressionMilestoneStatus.cs` | Enum статусов. |
| `client/Assets/Meta/Progression/Progression.cs` | Сервис, собирающий модель из бэкенд-проекций. |
| `client/Assets/Meta/MetaServicesExtensions.cs` | Регистрация `IProgression` в DI. |
| `client/Assets/Meta/Connection/BackendProjection.cs` | Паттерн прослушивания бэкенд-проекций. |
| `client/Assets/Meta/Cards/LootProgressionConfigs.cs` | Источник конфига лута. |
| `shared/Backend/SharedBackendUser.cs` | Содержит `ProgressionProjection` и `LootProjection`. |
| `client/Meta.csproj` | Нужно добавить новые файлы. |
| `client/Menu.csproj` | Без изменений — новые UI-файлы не добавлялись. |

### Документация к прочтению

- `.agents/docs/COMMON_CONTAINER.md` — паттерн `MonoBehaviour` + `ISceneService` + `IScopeSetup` + `Create()` + `[Inject]`; регистрация в VContainer.
- `.agents/docs/COMMON_LIFETIMES.md` — правило: каждый `Advise`/`View`/`ListenClick` требует `Lifetime`.
- `.agents/docs/COMMON_REACTIVE_BASICS.md` — `EventSource`, `ViewableProperty`, подписки.
- `.agents/docs/COMMON_REACTIVE_VALUES.md` — паттерны работы с `ViewableProperty`.
- `.agents/docs/COMMON_REACTIVE_COLLECTIONS.md` — `ViewableList` для реактивных коллекций.
- `.agents/docs/API_DESIGN_FULL.md` — `UniTask`, асинхронные методы, `IReadOnlyList`.
- `.agents/docs/CODE_STYLE_FULL.md` — порядок членов, `_camelCase`, `NoAwait`, скобки.
- `.agents/docs/GAMEPLAY.md` — игровой лут, карты, `CardType` (контекст для `LootBoxChoicePanel`).

### Риски

1. **Конфликт имён** между `Menu.Screens.ProgressionMilestone` (MonoBehaviour) и `Meta.Progression.ProgressionMilestone` (модель). Решение: UI-класс оставлен в `Menu.Screens`, в `MenuProgression` используется интерфейс `Meta.Progression.IProgressionMilestone`, конфликта имён не возникает.
2. **Текущий `ProgressionMilestone.cs` не компилируется** — использует `_lockedColor` / `_availableColor` / `_claimedColor` / `_reachedColor`, которых нет в SerializeField. Решение: заменить на логику спрайтов `_taken`/`_active`/`_locked`.
3. **Уведомление UI о смене списка майлстоунов** при обновлении конфига. Решение: expose `IViewableList<IProgressionMilestone>` — он наследует `IReadOnlyList` и уведомляет о добавлении/удалении элементов.
4. **Граница открытия лутбокса** — UI всё ещё создаёт `LootBoxChoicePanel`, но бэкенд-запросы уходят в `IProgression`. Это сохраняет "view-only" для `MenuProgression` и не дублирует `ICardsRegistry` в UI.
