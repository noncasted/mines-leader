## Задача: Connected Users Monitor — кто сейчас онлайн

### Цель
Добавить виджет или секцию на Home-странице консоли, показывающую подключенных пользователей в реальном времени: количество, список с именами, время подключения.

### Контекст
`IConnectedUsers` уже отслеживает все активные подключения в MetaGateway: `IViewableDelegate<IUserSession> Connected`, `IReadOnlyDictionary<Guid, IUserSession> Entries`, `IsConnected(Guid)`. Данные живут в MetaGateway (не Console). Нужен механизм передачи.

`IUserSession` содержит: `Guid UserId`, `IReadOnlyLifetime Lifetime`.

### Шаги реализации

**1. Создать DTO для передачи данных**
  1.1. Создать `ConnectedUserInfo`: `Guid UserId`, `string Name`, `DateTime ConnectedAt` — нужно добавить время подключения в `IUserSession` или `ConnectedUsers`

**2. Добавить API для получения списка**
  2.1. Вариант A (простой): HTTP endpoint в MetaGateway `/api/connected-users` возвращающий список
  2.2. Вариант B (reactive): Cluster-level `AddressableState<ConnectedUsersState>` обновляемый из MetaGateway
  2.3. Для MVP — вариант A, polling каждые 5 секунд

**3. Расширить PlayersWidget или создать отдельный виджет**
  3.1. Вариант A: Добавить колонку "Online" в существующий `PlayersWidget.razor` — `backend/Console/Pages/Home/PlayersWidget.razor`
  3.2. Вариант B: Создать отдельный `ConnectedUsersWidget.razor` — `backend/Console/Pages/Home/ConnectedUsersWidget.razor` [новый файл]
  3.3. Рекомендуется вариант B — не смешивать concerns; виджет показывает: count online, список с именами, индикатор статуса

**4. Добавить виджет на Home**
  4.1. Добавить `ConnectedUsersWidget` в `Home.razor` — `backend/Console/Pages/Home/Home.razor`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Orchestration/MetaGateway/UserFlow/ConnectedUsers.cs` | Источник данных — Entries + Connected event |
| `backend/Console/Pages/Home/ConnectedUsersWidget.razor` | Новый виджет [новый файл] |
| `backend/Console/Pages/Home/Home.razor` | Встраивание виджета |
| `backend/Console/Pages/Home/PlayersWidget.razor` | Референс — похожий виджет с таблицей |

### Документация к прочтению
- `rules/BLAZOR.md` — UiComponent для reactive подписок, @code block order
- `rules/REACTIVE.md` — IViewableDelegate подписка

### Риски
- **Cross-service**: ConnectedUsers живет в MetaGateway. Если Console и MetaGateway в одном процессе (Aspire), можно инжектить напрямую. Если нет — HTTP endpoint
- **Имена пользователей**: UserCollection доступна в Console для резолва Guid -> Name
- **ConnectedAt**: `IUserSession` не хранит время подключения — нужно добавить поле или трекать отдельно
