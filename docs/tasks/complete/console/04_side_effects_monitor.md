## Задача: Side Effects Monitor — визуализация очередей side effects

### Цель
Создать страницу консоли для мониторинга side effects pipeline: размеры очередей (queue, processing, retry), последние ошибки, возможность ручного retry/drop failed эффектов.

### Контекст
`ISideEffectsStorage` уже имеет все нужные методы: `Read()`, `CompleteProcessing()`, `FailProcessing()`, `RequeueReady()`, `RequeueStuck()`. `SideEffectsWorker` записывает метрику `BackendMetrics.SideEffectQueueDepth`. Три таблицы PostgreSQL: `side_effects_queue`, `side_effects_processing`, `side_effects_retry_queue`. Нет UI для просмотра.

### Шаги реализации

**1. Добавить stats-методы в ISideEffectsStorage**
  1.1. Добавить `GetQueueCounts()` возвращающий размеры трех таблиц — `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs`
    ```
    SideEffectsStats { int QueueCount, int ProcessingCount, int RetryCount }
    ```
  1.2. SQL: `SELECT COUNT(*) FROM side_effects_queue` для каждой таблицы (одним запросом)
  1.3. Добавить `GetRetryEntries(int limit)` — для отображения failed эффектов с типом и retry_count

**2. Добавить возможность ручного управления**
  2.1. Добавить `DropEntry(Guid id)` — удалить конкретный entry из retry_queue — `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs`
  2.2. Добавить `RequeueEntry(Guid id)` — переместить из retry обратно в queue

**3. Создать Blazor страницу**
  3.1. Создать `backend/Console/Pages/SideEffects/SideEffectsMonitor.razor` [новый файл — добавить в Console.csproj]
  3.2. Route: `/side-effects`
  3.3. Три карточки-счетчика: Queue (pending), Processing (in-flight), Retry (failed)
  3.4. Таблица retry entries: Id (short), Type (side effect class name), RetryCount, RetryAfter, кнопки Retry/Drop
  3.5. Кнопки: "Requeue All Ready", "Requeue Stuck" (вызов существующих методов)
  3.6. Auto-refresh каждые 5 секунд

**4. Интеграция в навигацию**
  4.1. Добавить роут в `ConsoleConstants.Pages` — `backend/Console/Common/ConsoleConstants.cs`
  4.2. Добавить карточку в `HomeInfrastructureSection.razor` — `backend/Console/Pages/Home/HomeInfrastructureSection.razor`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` | Добавить stats + управление |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | Референс — как worker использует storage |
| `backend/Console/Pages/SideEffects/SideEffectsMonitor.razor` | Новая страница [новый файл] |
| `backend/Console/Common/ConsoleConstants.cs` | Роут |
| `backend/Console/Pages/Home/HomeInfrastructureSection.razor` | Карточка на Home |

### Документация к прочтению
- `rules/BLAZOR.md` — early return, loading spinner, error handling с ToastService
- `rules/CODE_STYLE.md` — member order

### Риски
- **COUNT(*) производительность**: на больших таблицах может быть медленно. Для MVP приемлемо — таблицы side effects обычно малы. При необходимости — кэшировать counts
- **ISideEffectsStorage — singleton**: Console и Silo разделяют storage через DI. Storage читает из PostgreSQL — доступен из любого сервиса
- **Concurrent modification**: RequeueStuck и RequeueReady уже thread-safe (атомарные SQL операции)
