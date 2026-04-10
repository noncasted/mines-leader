## Infrastructure Changelog — 2026-04-10

Три инфраструктурные задачи реализованы параллельно командой из 3 executor агентов.

---

### 1. Distributed Tracing

**Цель:** Добавить Activity spans во все ключевые инфраструктурные подсистемы. До этого было определено 2 ActivitySource, но ни один не использовался — трейсы были пусты.

**Новые ActivitySource** (`backend/Common/Extensions/Traces/TraceExtensions.cs`):
- `Infrastructure.Transactions`
- `Infrastructure.SideEffects`
- `Infrastructure.Messaging.DurableQueue`
- `Infrastructure.Messaging.RuntimePipe`
- `Infrastructure.Messaging.RuntimeChannel`
- `Infrastructure.TaskBalancer`

Все добавлены в `AllSources` — автоматически подхватываются OTEL pipeline через `ConfigureOpenTelemetry()`.

**Инструментированные операции:**

| Компонент | Span | Теги | Error status |
|-----------|------|------|-------------|
| Transactions | `Transaction.Process` | `transaction.id`, `participant.count` | Да — при Rollback |
| SideEffects | `SideEffect.Execute` | `side_effect.retry_count`, `side_effect.correlation_id` | — |
| DurableQueue | `DurableQueue.Push` | `message.type`, `observer.count` | — |
| RuntimePipe | `RuntimePipe.Send` | `message.type`, `pipe.timeout` | Да — при TimeoutException и Exception |
| RuntimeChannel | `RuntimeChannel.Publish` | `message.type`, `observer.count` | — |
| TaskBalancer | `TaskBalancer.Execute` | `task.priority`, `task.score` | Да — при Exception |

**Измененные файлы:**

| Файл | Изменение |
|------|-----------|
| `backend/Common/Extensions/Traces/TraceExtensions.cs` | +6 ActivitySource, обновлен AllSources |
| `backend/Infrastructure/Orleans/Transactions/Transactions.cs` | Span в Process(), Error status в Rollback() |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | Span в ExecuteEntry() |
| `backend/Infrastructure/Messaging/Queues/DurableQueue.cs` | Span в Push() |
| `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs` | Span в Send(), Error status в catch |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs` | Span в Publish() |
| `backend/Infrastructure/Execution/TaskScheduling/TaskBalancer.cs` | Span в Execute(), Error status в catch |

---

### 2. Graceful Shutdown

**Цель:** Обеспечить корректное завершение in-flight операций при остановке сервисов. До этого StopAsync был пустым, а ошибки Rollback молча проглатывались.

**Изменения:**

**SideEffectsWorker** (`backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`):
- Добавлен `CancellationTokenSource _shutdownCts`
- Loop проверяет `_shutdownCts.IsCancellationRequested` перед запуском новых задач и в foreach по entries
- `StopAsync` переписан: отменяет `_shutdownCts`, polling `_inProgress == 0` с timeout 30 сек
- Warning-лог при превышении timeout: `[SideEffects] Shutdown timeout exceeded, {InProgress} effects still in progress`

**Transactions.Rollback** (`backend/Infrastructure/Orleans/Transactions/Transactions.cs`):
- Пустой `catch (Exception e) { }` заменен на:
  ```csharp
  _logger.LogError(e, "[Transactions] Failed to rollback participant {ParticipantId}", participantId);
  BackendMetrics.TransactionRollbackFailure.Add(1);
  ```
- Переменная loop `_` переименована в `participantId` для логирования

**BackendMetrics** (`backend/Common/Extensions/Metrics/BackendMetrics.cs`):
- Добавлен `TransactionRollbackFailure` Counter (`backend.transactions.rollback_failure`)

**Aspire** (`backend/Orchestration/Aspire/Program.cs`):
- Добавлен `builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30))`
- Добавлены `using Microsoft.Extensions.DependencyInjection` и `using Microsoft.Extensions.Hosting`

**ClusterParticipantStartup** — проверен, корректно завершается через lifetime pattern, изменений не потребовалось.

**Измененные файлы:**

| Файл | Изменение |
|------|-----------|
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | _shutdownCts, StopAsync с ожиданием, проверка в Loop |
| `backend/Infrastructure/Orleans/Transactions/Transactions.cs` | Логирование + метрика в Rollback catch |
| `backend/Common/Extensions/Metrics/BackendMetrics.cs` | +TransactionRollbackFailure counter |
| `backend/Orchestration/Aspire/Program.cs` | +HostOptions.ShutdownTimeout = 30s |

---

### 3. Benchmark Regression Detection

**Цель:** Автоматическое обнаружение регрессий производительности. До этого результаты бенчмарков хранились и отображались, но не сравнивались автоматически.

**Новый файл: BenchmarkComparison.cs** (`backend/Tools/Benchmarks/Common/BenchmarkComparison.cs`):
- `MetricDirection` enum: `HigherIsBetter` (throughput, ops/s), `LowerIsBetter` (latency, ms)
- `BenchmarkComparison.Compare(current, baseline, direction, threshold)` — вычисляет delta и флаг регрессии
- Throughput regression: `current < baseline * (1 - threshold)`
- Latency regression: `current > baseline * (1 + threshold)`
- `BenchmarkComparisonResult`: `BaselineMetricValue`, `RegressionPercent`, `IsRegression`

**Baseline pinning:**
- `BenchmarkState` (`BenchmarkMetricsHandle.cs`): +4 поля с `[Id(6)]`..`[Id(9)]`:
  - `[Id(6)] IsBaseline: bool`
  - `[Id(7)] BaselineMetricValue: double`
  - `[Id(8)] RegressionPercent: double`
  - `[Id(9)] IsRegression: bool`
- `BenchmarkStorage` (`BenchmarkStorage.cs`): +2 метода:
  - `GetById(Guid id)` — поиск run по ID через ReadAll
  - `GetBaseline(string benchmarkName)` — получение последнего baseline run

**Автоматическое сравнение** (`BenchmarkRoot.cs`):
- После завершения run перед сохранением: получает baseline, вычисляет comparison
- Записывает `BaselineMetricValue`, `RegressionPercent`, `IsRegression` в state
- Логирует warning при обнаружении регрессии: `[BenchmarkRunner] Regression detected for {Title}: {Percent:F1}% vs baseline`

**Configuration** (`BenchmarkOptions.cs`):
- `RegressionThreshold = 0.10` (10%)

**API endpoints** (`BenchmarkEndpoints.cs`):
- `POST /api/benchmarks/{id}/set-baseline` — пометить run как baseline (снимает предыдущий baseline)
- `GET /api/benchmarks/{title}/compare` — сравнить последний run с baseline
- `BenchmarkHistoryEntryDto`: +4 поля (IsBaseline, BaselineMetricValue, RegressionPercent, IsRegression)
- Новый DTO: `BenchmarkCompareDto` (Title, LatestId/Date/Metric, BaselineId/Date/Metric, RegressionPercent, IsRegression)

**Измененные файлы:**

| Файл | Изменение |
|------|-----------|
| `backend/Tools/Benchmarks/Common/BenchmarkComparison.cs` | Новый файл — логика сравнения |
| `backend/Tools/Benchmarks/Common/BenchmarkMetricsHandle.cs` | +4 поля в BenchmarkState [Id(6-9)] |
| `backend/Tools/Benchmarks/Common/BenchmarkOptions.cs` | +RegressionThreshold = 0.10 |
| `backend/Tools/Benchmarks/Common/BenchmarkStorage.cs` | +GetById(), +GetBaseline() |
| `backend/Tools/Benchmarks/Common/BenchmarkRoot.cs` | Comparison после run, warning при регрессии |
| `backend/Orchestration/ConsoleGateway/BenchmarkEndpoints.cs` | +set-baseline, +compare endpoints, +regression fields в DTO |

---

### 4. StateStorage Refactor: Request Objects + Slim Interface

**Цель:** Уменьшить растущий интерфейс `IStateStorage`, ввести request-объекты для Write/Delete, вынести convenience-методы в extensions.

**Изменения:**
- `IStateStorage`: 8 методов → 5 (Read, ReadBatch, ReadAll, Write(StateWriteRequest), Delete(StateDeleteRequest))
- Новые типы: `StateWriteRequest`, `StateDeleteRequest`
- `ReadRaw` — стал `private` в `StateStorage`
- Convenience-методы вынесены в `StateStorageExtensions` — обратная совместимость для всех consumer'ов

**Измененные файлы:**

| Файл | Изменение |
|------|-----------|
| `backend/Infrastructure/Orleans/State/StateStorage.cs` | Request types, новый интерфейс, рефакторинг implementation |
| `backend/Infrastructure/Orleans/State/StateStorageExtensions.cs` | Convenience-методы, алиас Read для ReadBatch |

**Consumer impact:** ZERO — все существующие вызовы сохраняют сигнатуру через extensions.

Подробности: → [state_storage_refactor.md](state_storage_refactor.md)

---

### Побочные изменения (сделаны воркерами вне основного плана)

**BackendMetrics** — дополнительные метрики:
- `DurableQueueNoSubscribers` — push при отсутствии подписчиков
- `ChannelDeliveryTimeout` — таймаут доставки observer'у
- `ChannelCatchUpExecuted` — выполненные catch-up операции
- `ChannelCatchUpMessages` — сообщений за catch-up
- `ChannelGapDetected` — обнаруженные пропуски последовательности
- `PipeRetry` — повторные попытки отправки pipe

**RuntimeChannel** — добавлена система catch-up:
- `SequencedMessage` — сообщение с порядковым номером
- `CatchUpResult` — результат catch-up (сообщения, наличие пропусков, текущий sequence)
- `CatchUp(observerId, lastSeenSequence)` — метод для получения пропущенных сообщений
- Кольцевой буфер размером `CatchUpBufferSize`
- Delivery timeout для observer'ов

**DurableQueue** — валидация подписчиков:
- `NoSubscribersException` — бросается при push без активных подписчиков
- Метрика `DurableQueueNoSubscribers`

**SideEffectsWorker** — stuck detection:
- Поле `_lastStuckCheck` для периодической проверки застрявших side effects
- Вызов `_storage.RequeueStuckOlderThan()` по интервалу `StuckCheckIntervalSeconds`

---

### Сводка

| Метрика | Значение |
|---------|----------|
| Файлов изменено | 19 (из инфра-задач) |
| Новых файлов | 1 (BenchmarkComparison.cs) |
| Новых ActivitySource | 6 |
| Новых метрик | 8 |
| Новых API endpoints | 2 |
| Новых полей в State | 4 (BenchmarkState) |
