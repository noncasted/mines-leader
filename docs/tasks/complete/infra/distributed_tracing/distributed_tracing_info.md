## Задача: Расширение Distributed Tracing

### Цель
Добавить Activity spans во все ключевые инфраструктурные подсистемы: транзакции, side effects, messaging (3 типа), task balancer. Сейчас определены 2 ActivitySource (`Player.Endpoints`, `Player.Connection`), но ни один не используется — нет ни одного вызова `.StartActivity()`. OpenTelemetry pipeline полностью настроен (OTLP exporter, Aspire dashboard), метрики собираются, но трейсы пусты. Невозможно проследить путь запроса через систему.

### Контекст
- OTEL пакеты подключены (`Directory.Packages.props`: 7 пакетов OpenTelemetry 1.15.0)
- `ConfigureOpenTelemetry()` в `ServiceDefaultsExtensions.cs` уже регистрирует все ActivitySource из `TraceExtensions.AllSources`
- Метрики уже покрывают все подсистемы (30+ метрик в `BackendMetrics.cs`) — трейсы должны дополнить их
- `TransactionContext` имеет `Id: Guid` — готовый correlation ID для привязки к Activity
- Aspire dashboard принимает OTLP — трейсы появятся автоматически после добавления spans

### Шаги реализации

**1. Добавить новые ActivitySource в TraceExtensions**
  1.1. Добавить ActivitySource для каждой подсистемы — `backend/Common/Extensions/Traces/TraceExtensions.cs`
       - `Transactions` — для транзакций
       - `SideEffects` — для side effects worker
       - `Messaging.DurableQueue` — для durable queue
       - `Messaging.RuntimePipe` — для runtime pipe
       - `Messaging.RuntimeChannel` — для runtime channel
       - `TaskBalancer` — для task scheduling
  1.2. Добавить все новые sources в `AllSources` коллекцию (автоматически подхватятся OTEL)

**2. Инструментировать транзакции**
  2.1. В `Transactions.Execute()` — обернуть выполнение в Activity span с тегами `transaction.id`, `participant.count` — `backend/Infrastructure/Orleans/Transactions/Transactions.cs`
  2.2. В `Transactions.Rollback()` — добавить span с `Status = Error` — `backend/Infrastructure/Orleans/Transactions/Transactions.cs`
  2.3. Пробросить `Activity.Current` через `TransactionContext` для корреляции — `backend/Infrastructure/Orleans/Transactions/TransactionContext.cs`

**3. Инструментировать SideEffects**
  3.1. В `SideEffectsWorker.ExecuteEntry()` — span на каждый side effect с тегами `side_effect.id`, `side_effect.retry_count` — `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`
  3.2. В основном Loop — span на каждый цикл сканирования — `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`

**4. Инструментировать Messaging**
  4.1. DurableQueue — span на push и consume — `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`, `backend/Infrastructure/Messaging/Queues/DurableQueueClient.cs`
  4.2. RuntimePipe — span на send/receive с `pipe.timeout` тегом — `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs`, `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs`
  4.3. RuntimeChannel — span на publish — `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs`, `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs`

**5. Инструментировать TaskBalancer**
  5.1. В `ExecuteLoop` — span на выполнение задачи с тегами `task.priority`, `task.score` — `backend/Infrastructure/Execution/TaskScheduling/TaskBalancer.cs`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Common/Extensions/Traces/TraceExtensions.cs` | Определение всех ActivitySource |
| `backend/Orchestration/Extensions/ServiceDefaultsExtensions.cs` | OTEL конфигурация (уже настроена) |
| `backend/Infrastructure/Orleans/Transactions/Transactions.cs` | Execute + Rollback — основные точки трейсинга |
| `backend/Infrastructure/Orleans/Transactions/TransactionContext.cs` | Correlation ID (context.Id) |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | Loop + ExecuteEntry |
| `backend/Infrastructure/Messaging/Queues/DurableQueue.cs` | Durable queue spans |
| `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs` | Pipe request/response spans |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs` | Channel pub/sub spans |
| `backend/Infrastructure/Execution/TaskScheduling/TaskBalancer.cs` | Task execution spans |
| `backend/Common/Extensions/Metrics/BackendMetrics.cs` | Справка по существующим метрикам |

### Документация к прочтению
- `rules/CODE_STYLE.md` — member order, naming conventions
- `rules/ORLEANS_GRAINS.md` — паттерн grain для понимания контекста транзакций

### Риски
- **Performance overhead**: Activity создаёт аллокации. В hot paths (messaging, task balancer) нужен head-based sampling (1-5% в production). Проверить через бенчмарки до/после.
- **High cardinality tags**: Не добавлять grain ID или message payload как теги — только структурные метаданные (type, count, status).
- **Context propagation**: `AsyncLocal<TransactionContext>` и `Activity.Current` оба используют AsyncLocal — убедиться что не конфликтуют.
