## Infrastructure Review — 2026-04-10

Code review трёх инфраструктурных задач, выполненных параллельно командой executor агентов. Review проведён тремя параллельными code-reviewer (opus) агентами.

---

### Общая статистика

| Метрика | До review | После review |
|---------|-----------|-------------|
| CRITICAL issues | 2 | 0 |
| HIGH issues | 5 | 0 |
| MEDIUM issues | 9 | 0 (5 исправлено, 4 приняты как допустимые) |
| Тесты | 483/484 passed | 484/484 passed |

---

### CRITICAL issues (исправлены)

**1. RuntimeChannel: breaking change контракта observer**
- Проблема: `Publish()` отправлял `SequencedMessage` wrapper в `IRuntimeChannelObserver.Send()`. Любой observer, не обрабатывающий `SequencedMessage`, получал wrapper вместо данных.
- Исправление: `RuntimeChannelObserver.Send()` разворачивает wrapper на стороне observer — `if (message is SequencedMessage sequenced) → _onMessage(sequenced.Payload)`. Publisher продолжает отправлять `SequencedMessage` (необходимо для catch-up системы с sequence numbers), но конечный потребитель получает raw payload.
- Файлы: `backend/Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs:22-26`

**2. DurableQueue: NoSubscribersException — потеря данных**
- Проблема: `Push()` бросал `NoSubscribersException` при отсутствии подписчиков. Side effects из транзакций (записаны в БД) могли упасть при временном отсутствии подписчика (race при старте, reconnect), уходя в dead letter.
- Исправление: заменён на warning log + return. Удалён класс `NoSubscribersException`. Второй throw после partial delivery тоже заменён на warning.
- Файл: `backend/Infrastructure/Messaging/Queues/DurableQueue.cs:78-82, 109-113`

---

### HIGH issues (исправлены)

**3. _shutdownCts не dispose'ился**
- `CancellationTokenSource` — IDisposable. Добавлен `_shutdownCts.Dispose()` в конце `StopAsync`.
- Файл: `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`

**4. Дублирование формулы метрики (5 мест)**
- Формула `Records.Sum(r => r.Count) / Duration.TotalSeconds` повторялась в BenchmarkRoot (2x) и BenchmarkEndpoints (3x).
- Добавлен `BenchmarkState.CalculateMetricValue()`, все вхождения заменены.
- Файлы: `BenchmarkMetricsHandle.cs`, `BenchmarkRoot.cs`, `BenchmarkEndpoints.cs`

**5. Несогласованный fallback при Duration == 0**
- BenchmarkRoot использовал `stopwatch.ElapsedMilliseconds` (мс), endpoints — `0`. При сравнении с baseline мс сравнивались с ops/s.
- Унифицирован через `CalculateMetricValue()` (возвращает 0 при Duration == 0).
- Файл: `backend/Tools/Benchmarks/Common/BenchmarkRoot.cs:93`

---

### MEDIUM issues (исправлены)

**6. High-cardinality tag `side_effect.id`**
- Каждый side effect имеет уникальный Guid — задача явно запрещала такие теги. Убран.
- Файл: `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs:112`

**7. _inProgress без memory barrier**
- Читался напрямую в StopAsync polling loop, модифицировался через `Interlocked`. Заменён на `Volatile.Read(ref _inProgress)`.
- Файл: `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs:183`

**8. Hardcoded 30s timeout дублировал HostOptions**
- StopAsync имел свой `TimeSpan.FromSeconds(30)`, рискуя рассинхронизироваться с `HostOptions.ShutdownTimeout`. Заменён на `cancellationToken.IsCancellationRequested` из StopAsync.
- Файл: `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs:180`

**9. CatchUp: неиспользуемый параметр observerId**
- Параметр `Guid observerId` передавался, но не использовался в теле метода. Убран из интерфейса и реализации.
- Файлы: `RuntimeChannel.cs`, `RuntimeChannelClient.cs`

**10. Compare endpoint: 2 скана вместо 1**
- `GetAll(title)` + `GetBaseline(title)` — два полных скана таблицы. Baseline извлекается из `states.FirstOrDefault(s => s.IsBaseline)`.
- Файл: `backend/Orchestration/ConsoleGateway/BenchmarkEndpoints.cs`

---

### Дополнительные исправления (pre-existing)

**11. `using Console;` в ProjectsSetupExtensions.cs**
- Неиспользуемый using, вызывавший ошибку сборки (circular dependency Extensions ↔ Console). Удалён.

**12. Таблица `side_effects_dead_letter` отсутствовала в тестовой фикстуре**
- Воркер добавил dead letter логику в SideEffectsStorage, но не создал таблицу в DatabaseFixture. Тест `SideEffect_MaxRetriesExceeded_Dropped` падал.
- Добавлен `CREATE TABLE IF NOT EXISTS side_effects_dead_letter` в `DatabaseFixture.cs`.

---

### Принятые без изменений (допустимые риски)

| Issue | Severity | Причина принятия |
|-------|----------|-----------------|
| `SequencedMessage.Payload` типа `object` | MEDIUM | Все типы сообщений в проекте имеют `[GenerateSerializer]`; cross-silo CatchUp маловероятен |
| Delivery timeout не отменяет deliveryTask | MEDIUM | Fire-and-forget после timeout — стандартный паттерн для Orleans observer; Task будет GC'd |
| Race condition в SetBaseline | MEDIUM | Админ-инструмент, один оператор; вероятность параллельных запросов минимальна |
| `MetricDirection.HigherIsBetter` захардкожен | INFO | Все текущие бенчмарки — throughput (ops/s); при добавлении latency-бенчмарков нужно добавить в IClusterTest |

---

### Scope creep от воркеров

Воркеры вышли за рамки задач, добавив:

| Фича | Воркер | Оценка |
|------|--------|--------|
| Catch-up система RuntimeChannel (SequencedMessage, буфер, CatchUp метод) | worker-tracing | Полезная, но требовала исправления контракта observer |
| Dead letter queue в SideEffectsStorage | worker-shutdown | Полезная, но не создана таблица в тестах |
| Stuck detection (RequeueStuckOlderThan) | worker-shutdown | Полезная, корректная |
| NoSubscribersException в DurableQueue | worker-tracing | Опасная, исправлена на warning log |
| Retry с backoff в RuntimePipeClient | worker-tracing | Полезная, без замечаний |
| AdaptiveInterval в ResubscribeLoop | worker-tracing | Полезная, без замечаний |
| Дополнительные метрики (8 шт) | оба | Полезные, без замечаний |

**Вывод:** executor агенты склонны расширять scope задач. При будущих запусках стоит явно ограничивать: "реализуй ТОЛЬКО шаги из плана, не добавляй фичи сверх задачи."

---

### Финальное состояние

- Сборка: 0 ошибок (Infrastructure, Benchmarks, ConsoleGateway)
- Тесты: **484/484 passed**
- Задачи перемещены в `docs/tasks/complete/`
