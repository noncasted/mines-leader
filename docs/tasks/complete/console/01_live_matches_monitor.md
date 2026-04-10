## Задача: Live Matches Monitor — мониторинг активных матчей в реальном времени

### Цель
Создать страницу консоли для отображения всех активных игровых сессий в реальном времени. Администратор должен видеть: кто играет, тип матча, длительность, и иметь возможность перейти к деталям матча.

### Контекст
Сейчас консоль не показывает что происходит на сервере прямо сейчас. `SessionsCollection` уже хранит все активные сессии в памяти (`Dictionary<Guid, ISession>`), но нет UI для просмотра. Данные живут в Game Gateway (не в Console Gateway), поэтому нужен механизм передачи данных между сервисами.

### Шаги реализации

**1. Расширить ISession / SessionData для передачи метаданных**
  1.1. Добавить в `ISession` или `SessionData` поля для отображения: `SessionType`, `DateTime CreatedAt`, список участников (user IDs) — `backend/Game/Session/Root/Session.cs`
  1.2. Создать DTO `SessionOverview` с полями: `Guid Id`, `SessionType Type`, `DateTime CreatedAt`, `IReadOnlyList<Guid> Participants` — `backend/Game/Session/Root/Session.cs` или отдельный файл

**2. Добавить API для получения списка сессий**
  2.1. Расширить `ISessionsCollection` методом `GetOverviews()` возвращающим `IReadOnlyList<SessionOverview>` — `backend/Game/Global/SessionsCollection.cs`
  2.2. Создать endpoint в ConsoleGateway для получения сессий. Проблема: SessionsCollection живет в Game Gateway. Варианты решения:
    - Вариант A: Messaging pipe между Console и Game Gateway
    - Вариант B: Grain-based `ISessionsOverviewGrain` доступный из обоих gateways
    - Вариант C: Периодический push из Game в Cluster-level state

**3. Создать Blazor страницу Live Matches**
  3.1. Создать `backend/Console/Pages/Matches/LiveMatches.razor` [новый файл — добавить в Console.csproj]
  3.2. Route: `/matches`
  3.3. Таблица: Id (short), Type (Match/Lobby), Players (имена через UserCollection), Duration (DateTime.UtcNow - CreatedAt), кнопка перехода к деталям
  3.4. Auto-refresh каждые 5 секунд через `Timer` или reactive подписка
  3.5. Наследовать `UiComponent` если используется reactive подписка

**4. Интеграция в навигацию**
  4.1. Добавить роут в `ConsoleConstants.Pages` — `backend/Console/Common/ConsoleConstants.cs`
  4.2. Добавить NavLink "Matches" в `MainLayout.razor` — `backend/Orchestration/ConsoleGateway/Shared/MainLayout.razor`
  4.3. Добавить карточку в `HomeGameSection.razor` — `backend/Console/Pages/Home/HomeGameSection.razor`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Game/Global/SessionsCollection.cs` | Источник данных — хранит все активные сессии |
| `backend/Game/Session/Root/Session.cs` | ISession интерфейс — нужны метаданные |
| `backend/Console/Pages/Matches/LiveMatches.razor` | Новая страница [новый файл] |
| `backend/Console/Common/ConsoleConstants.cs` | Роут `/matches` |
| `backend/Orchestration/ConsoleGateway/Shared/MainLayout.razor` | Навигация |
| `backend/Console/Pages/Home/HomeGameSection.razor` | Карточка на Home |

### Документация к прочтению
- `rules/BLAZOR.md` — early return, UiComponent, @code block order
- `rules/CODE_STYLE.md` — member order, naming

### Риски
- **Cross-gateway data access**: SessionsCollection живет в Game Gateway, Console — отдельный сервис. Нужно решить архитектуру передачи данных. Самый простой путь — grain, доступный из обоих gateway, который Game обновляет при добавлении/удалении сессий
- **Частота обновления**: polling каждые 5 секунд vs reactive push. Polling проще, но менее отзывчив
- **Имена игроков**: UserCollection доступна в Console для резолва Guid -> Name
