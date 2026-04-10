## Задача: Модальное окно инициализации кластера в консоли

### Цель
При входе на любую страницу консоли, если `ClusterParticipantContext.IsInitialized == false`, поверх контента отображается модальное окно с текущим этапом инициализации сервера. Окно автоматически скрывается после завершения инициализации.

### Стейджи инициализации (из `ClusterParticipantStartup`)
1. Waiting for Orleans
2. Starting Task Balancer
3. Starting Messaging
4. Starting Service Discovery
5. Waiting for other services
6. Running local setup loop
7. Waiting for coordinator
8. Initialized

### Шаги реализации

1. **Расширить `IClusterParticipantContext`** — добавить `ViewableProperty<string> CurrentStage` для трекинга текущего этапа инициализации — `backend/Infrastructure/Startup/ClusterParticipantContext.cs`

2. **Обновить `ClusterParticipantStartup`** — проставлять `CurrentStage` перед каждым этапом — `backend/Cluster/Coordination/ClusterParticipantStartup.cs`

3. **Создать компонент `StartupOverlay.razor`** — модальный оверлей с подпиской на `IsInitialized` и `CurrentStage`, inherits `UiComponent`. Показывает spinner + текущий стейдж. Скрывается когда `IsInitialized == true` — `backend/Orchestration/ConsoleGateway/Shared/StartupOverlay.razor`

4. **Встроить `<StartupOverlay />` в `MainLayout.razor`** — после `@Body`, перед провайдерами — `backend/Orchestration/ConsoleGateway/Shared/MainLayout.razor`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Infrastructure/Startup/ClusterParticipantContext.cs` | Контекст инициализации — добавляем `CurrentStage` |
| `backend/Cluster/Coordination/ClusterParticipantStartup.cs` | Процесс старта — проставляем стейджи |
| `backend/Orchestration/ConsoleGateway/Shared/MainLayout.razor` | Лейаут консоли — встраиваем оверлей |
| `backend/Orchestration/ConsoleGateway/Shared/MainLayout.razor.cs` | Code-behind лейаута |
| `backend/Console/Common/UiComponent.cs` | Базовый компонент с Lifetime — наследуем для оверлея |

### Документация к прочтению
- `rules/BLAZOR.md` — создаём reactive Blazor-компонент с `UiComponent`
- `rules/REACTIVE.md` — подписка через `View()` на `ViewableProperty`

### Риски
- `MainLayout` наследует `LayoutComponentBase`, а не `UiComponent`, поэтому оверлей нужно делать отдельным компонентом, а не встраивать подписку прямо в лейаут
- SDK-style csproj автоматически включает `.razor` файлы — ручное добавление в `.csproj` не требуется
