## Задача: Cluster Health — состояние кластера и сервисов

### Цель
Создать страницу или виджет для отображения состояния всех сервисов кластера: какие сервисы запущены, когда последний heartbeat, общий статус здоровья.

### Контекст
`IServiceDiscovery` уже отслеживает все сервисы: `IReadOnlyDictionary<Guid, IServiceOverview> Entries`. `IServiceOverview` содержит: `Guid Id`, `ServiceTag Tag` (Coordinator/Meta/Game/Silo), `DateTime UpdateTime`. Периодический push через messaging обновляет `UpdateTime`.

### Шаги реализации

**1. Проверить доступность ServiceDiscovery в Console**
  1.1. Проверить регистрацию `IServiceDiscovery` в Console DI — `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs`
  1.2. Если не зарегистрирован — добавить. ServiceDiscovery использует Messaging для подписки

**2. Создать Blazor виджет**
  2.1. Создать `backend/Console/Pages/Home/ClusterHealthWidget.razor` [новый файл — добавить в Console.csproj]
  2.2. Отображать: карточки по типу сервиса (Coordinator, Meta, Game, Silo), количество инстансов, последний UpdateTime
  2.3. Индикация: зеленый если UpdateTime < 30s ago, желтый если < 60s, красный если > 60s
  2.4. Наследовать `UiComponent`, подписаться на изменения ServiceDiscovery

**3. Опционально: отдельная страница с деталями**
  3.1. Создать `backend/Console/Pages/Cluster/ClusterHealth.razor` [новый файл] — полная таблица всех сервисов
  3.2. Route: `/cluster`
  3.3. Таблица: Service Id (short), Tag, Last Update, Status (healthy/warning/stale)
  3.4. Добавить роут в `ConsoleConstants.Pages`

**4. Интеграция**
  4.1. Добавить `ClusterHealthWidget` на Home — `backend/Console/Pages/Home/Home.razor`
  4.2. Добавить карточку в `HomeInfrastructureSection.razor` (если есть отдельная страница)

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Cluster/Discovery/ServiceDiscovery.cs` | Источник данных — Entries dictionary |
| `backend/Cluster/Discovery/IServiceOverview.cs` | DTO — Id, Tag, UpdateTime |
| `backend/Console/Pages/Home/ClusterHealthWidget.razor` | Новый виджет [новый файл] |
| `backend/Console/Pages/Home/Home.razor` | Встраивание виджета |
| `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs` | DI регистрация |

### Документация к прочтению
- `rules/BLAZOR.md` — UiComponent, reactive подписки

### Риски
- **ServiceDiscovery в Console**: нужно проверить, получает ли Console сервис messaging updates. Если Console не подписан на discovery channel — виджет будет пустой
- **Stale detection**: порог "здоровый/нездоровый" зависит от частоты heartbeat. Нужно узнать интервал push в ServiceDiscovery
