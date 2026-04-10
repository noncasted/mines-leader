## Задача: Matchmaking Dashboard — мониторинг очереди поиска матчей

### Цель
Создать страницу консоли для мониторинга состояния матчмейкинга: количество игроков в очереди по типам, среднее время ожидания, статистика созданных матчей (PvP vs Bot fallback).

### Контекст
Класс `Matchmaking` хранит `_searchQueue` — `Dictionary<GameMatchType, List<SearchQueueEntry>>` с `UserId` и `JoinedAt`. Данные живут в MetaGateway. Сейчас нет способа увидеть состояние очереди извне. Нужен API для чтения статистики.

### Шаги реализации

**1. Добавить stats API в Matchmaking**
  1.1. Создать DTO `MatchmakingStats` — `backend/Orchestration/MetaGateway/Matchmaking/Matchmaking.cs`
    ```
    - QueueSizes: Dictionary<GameMatchType, int>
    - OldestWaitTime: TimeSpan?
    - AverageWaitTime: TimeSpan?
    - TotalInQueue: int
    ```
  1.2. Добавить метод `GetStats()` в `IMatchmaking` — `backend/Orchestration/MetaGateway/Matchmaking/Matchmaking.cs`
  1.3. Реализовать: пройти по `_searchQueue`, собрать размеры, рассчитать время ожидания

**2. Добавить счетчики созданных матчей**
  2.1. Добавить поля-счетчики в `Matchmaking`: `_matchesCreated`, `_botMatchesCreated`, `_lobbiesCreated` — `backend/Orchestration/MetaGateway/Matchmaking/Matchmaking.cs`
  2.2. Инкрементировать при создании матча/лобби в соответствующих методах
  2.3. Включить в `MatchmakingStats`: `int MatchesCreated`, `int BotMatchesCreated`, `int LobbiesCreated`

**3. Передать данные в Console Gateway**
  3.1. Вариант: endpoint в MetaGateway + HTTP вызов из Console. Или: messaging pipe
  3.2. Проще всего: добавить HTTP endpoint `/api/matchmaking/stats` в MetaGateway
  3.3. В Console: `HttpClient` для запроса статистики (через Service Discovery для адреса MetaGateway)

**4. Создать Blazor страницу**
  4.1. Создать `backend/Console/Pages/Matchmaking/MatchmakingDashboard.razor` [новый файл — добавить в Console.csproj]
  4.2. Route: `/matchmaking`
  4.3. Карточки: Total in Queue, Average Wait Time, Matches Created, Bot Fallback %
  4.4. Таблица: по типам матчей — Queue Size, Oldest Entry, Matches Created
  4.5. Auto-refresh каждые 3-5 секунд

**5. Интеграция в навигацию**
  5.1. Добавить роут в `ConsoleConstants.Pages` — `backend/Console/Common/ConsoleConstants.cs`
  5.2. Добавить карточку в `HomeGameSection.razor` — `backend/Console/Pages/Home/HomeGameSection.razor`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Orchestration/MetaGateway/Matchmaking/Matchmaking.cs` | Источник данных — очередь + счетчики |
| `backend/Console/Pages/Matchmaking/MatchmakingDashboard.razor` | Новая страница [новый файл] |
| `backend/Console/Common/ConsoleConstants.cs` | Роут |
| `backend/Console/Pages/Home/HomeGameSection.razor` | Карточка на Home |
| `backend/Cluster/Discovery/ServiceDiscovery.cs` | Для получения адреса MetaGateway |

### Документация к прочтению
- `rules/BLAZOR.md` — паттерны страниц консоли
- `rules/CODE_STYLE.md` — member order

### Риски
- **Cross-service communication**: Matchmaking живет в MetaGateway, Console — отдельный процесс. HTTP endpoint самый простой, но нужен service discovery для адреса
- **Lock contention**: `GetStats()` должен аcquire `_lock` для чтения очереди — может блокировать матчмейкинг если запросы частые. Решение: snapshot данных вне lock, или read-only доступ (данные приблизительные — ок для мониторинга)
- **Счетчики не персистентны**: при перезапуске MetaGateway обнуляются. Для MVP это приемлемо
