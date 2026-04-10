# Messaging - Отчёт о выполненных задачах

10 задач в 3 фазах. Все завершены.

## Фаза 1: Надёжность доставки (P0)

### 01. Channel Sequence Catch-Up
**Проблема:** Потеря сообщений при смерти observer reference (сетевой сбой, перезагрузка silo).
**Решение:** Ring buffer (1024 msg) + sequence numbers + catch-up при реподписке.
- `SequencedMessage` обёртка с номером последовательности
- `CatchUp(lastSeenSequence)` метод на грейне - возвращает пропущенные + флаг gap
- Observer отслеживает `LastSeenSequence`, разворачивает `SequencedMessage.Payload`

**Файлы (подтверждено в diff):**
- `RuntimeChannel.cs` - ring buffer, _sequenceNumber, CatchUp()
- `RuntimeChannelClient.cs` - вызов CatchUp при реподписке
- `RuntimeChannelObserver.cs` - tracking LastSeenSequence
- `RuntimeChannelOptions.cs` - CatchUpBufferSize

### 02. DurableQueue No Subscribers
**Проблема:** Push() без подписчиков молча теряет сообщения.
**Решение:** Warning log если 0 observers (после ревью заменено с exception на log чтобы не ломать pipeline).

**Файлы:** `DurableQueue.cs`, `BackendMetrics.cs` (DurableQueueNoSubscribers counter)

### 03. Periodic Requeue Stuck
**Проблема:** Side effects застревают в processing при крэше silo.
**Решение:** Каждые 60с проверка, записи старше 5 мин перемещаются обратно в очередь.

**Файлы (подтверждено в diff):**
- `SideEffectsStorage.cs` - `RequeueStuckOlderThan(TimeSpan age)` с SQL CTE
- `SideEffectsWorker.cs` - периодический вызов с _lastStuckCheck
- `SideEffectsOptions.cs` - StuckCheckIntervalSeconds, StuckThresholdMinutes

---

## Фаза 2: Порядок и консистентность (P1)

### 04. StateCollection Idempotency
**Проблема:** Параллельный SideEffectsWorker может доставить Version 2 раньше Version 1.
**Решение:** Timestamp-based идемпотентность - `UpdatedAt` поле в `StateCollectionUpdate`, per-key tracking `_lastUpdated`, пропуск устаревших обновлений.

**Файлы (подтверждено):** `StateCollection.cs` - [Id(2)] DateTime UpdatedAt, _lastUpdated dict, conditional apply

### 05. Channel Observer Delivery Timeout
**Проблема:** Медленный observer блокирует broadcast всем остальным.
**Решение:** Per-observer timeout 5с в Publish(), удаление observer при таймауте.

**Файлы:** `RuntimeChannel.cs` - timeout в SendSafe, `RuntimeChannelOptions.cs` - DeliveryTimeoutSeconds

### 06. Pipe Retry with Backoff
**Проблема:** RuntimePipe caller падает при временной недоступности handler (реподписка ~10с).
**Решение:** Exponential backoff retry: 3 попытки с задержками 500ms/1s/2s.

**Файлы (подтверждено):** `MessagePipeClient.cs` - retry loop в Send(), `RuntimePipeOptions.cs` - SendRetryCount, SendRetryBaseDelayMs

---

## Фаза 3: Observability (P2)

### 07. Distributed Tracing - CorrelationId
**Решение:** CorrelationId поле через все стадии: transaction -> side effect -> queue -> delivery.

**Файлы (подтверждено):** `DurableQueueSideEffect.cs` - [Id(2)] CorrelationId, `DurableQueueClient.cs` - populate, `SideEffectsWorker.cs` - structured logging scope

### 08. Adaptive Resubscribe
**Решение:** Адаптивный интервал: успех 10s->60s плавно, неудача 1s exponential backoff, +/- 20% jitter.

**Файлы:** `RuntimeChannelClient.cs`, `DurableQueueClient.cs`, `MessagePipeClient.cs` - AdaptiveInterval

### 09. Dead Letter Queue
**Проблема:** Сообщения после исчерпания ретраев молча удалялись.
**Решение:** Перемещение в таблицу `side_effects_dead_letter` с error message и timestamp.

**Файлы (подтверждено):** `SideEffectsStorage.cs` - FailProcessing() теперь пишет в dead letter вместо DELETE

### 10. Messaging Dashboard
**Решение:** Blazor-страница с 4-column grid: side effects, channels, pipes, dead letters.

---

## Итого

- 18 файлов изменено + 1 новый utility class
- 7 новых метрик (CatchUp x3, DeliveryTimeout, NoSubscribers, PipeRetry, DeadLetter)
- 1 новая SQL таблица (side_effects_dead_letter)
- Обратная совместимость полная
