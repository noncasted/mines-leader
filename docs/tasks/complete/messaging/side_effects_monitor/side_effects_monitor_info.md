## Задача: Side Effects Monitor — мониторинг очередей side effects

### Цель
Создать страницу консоли `/side-effects` для мониторинга side effects pipeline:
1. Карточки-счетчики: Queue (pending), Processing (in-flight), Retry (failed)
2. Таблица retry entries: Id, Type, RetryCount, RetryAfter, кнопки Retry/Drop
3. Кнопки: "Requeue All Ready", "Requeue Stuck"
4. Auto-refresh каждые 5 секунд

### Контекст
`ISideEffectsStorage` доступен в Console через DI (Console.csproj -> Infrastructure.csproj). Три PostgreSQL-таблицы: `side_effects_queue`, `side_effects_processing`, `side_effects_retry_queue`. Нужно добавить stats-методы в storage и создать Blazor страницу.

### Шаги реализации

**1. Добавить stats и management методы в ISideEffectsStorage**
  1.1. Добавить DTO `SideEffectsStats` (QueueCount, ProcessingCount, RetryCount) и `RetryQueueEntry` (Id, TypeName, RetryCount, RetryAfter) — `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs`
  1.2. Добавить `Task<SideEffectsStats> GetStats()` в интерфейс и реализацию — один SQL запрос с subselects
  1.3. Добавить `Task<IReadOnlyList<RetryQueueEntry>> GetRetryEntries(int limit)` — SELECT из retry_queue
  1.4. Добавить `Task DropRetryEntry(Guid id)` — DELETE из retry_queue
  1.5. Добавить `Task RequeueRetryEntry(Guid id)` — перемещение из retry_queue в queue

**2. Создать Blazor страницу SideEffectsMonitor**
  2.1. Создать `backend/Console/Pages/SideEffects/SideEffectsMonitor.razor` [новый файл]
  2.2. Route: `/side-effects`, наследовать ComponentBase
  2.3. Три карточки-счетчика с иконками
  2.4. Таблица retry entries с кнопками Retry/Drop
  2.5. Кнопки "Requeue Ready" и "Requeue Stuck"
  2.6. Timer auto-refresh каждые 5 секунд

**3. Интеграция в навигацию**
  3.1. Добавить `SideEffects = "/side-effects"` в `ConsoleConstants.Pages` — `backend/Console/Common/ConsoleConstants.cs`
  3.2. Добавить карточку в `HomeInfrastructureSection.razor` — `backend/Console/Pages/Home/HomeInfrastructureSection.razor`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` | Добавить stats + management методы |
| `backend/Console/Pages/SideEffects/SideEffectsMonitor.razor` | Новая страница [новый файл] |
| `backend/Console/Common/ConsoleConstants.cs` | Роут |
| `backend/Console/Pages/Home/HomeInfrastructureSection.razor` | Карточка на Home |

### Документация к прочтению
- `rules/BLAZOR.md` — early return, loading spinner, @code block order
- `rules/CODE_STYLE.md` — member order

### Риски
- COUNT(*) на больших таблицах — для side effects обычно малы, приемлемо
- Timer dispose — нужен IDisposable для cleanup
