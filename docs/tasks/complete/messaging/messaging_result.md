## Messaging Infrastructure Roadmap — Результат

### Статус: Завершено (все 10 задач, все 3 фазы)

### Что сделано

**Фаза 1 — Надёжность доставки (P0):**
1. RuntimeChannel: sequence numbers, ring buffer (1024), catch-up при resubscribe, gap detection
2. DurableQueue: NoSubscribersException при 0 observers или all-fail, интеграция с retry через SideEffectsWorker
3. SideEffectsWorker: периодический RequeueStuckOlderThan (каждые 60с, порог 5 мин)

**Фаза 2 — Ordering и consistency (P1):**
4. StateCollection: timestamp-based idempotency (UpdatedAt в StateCollectionUpdate, conditional apply)
5. RuntimeChannel: per-observer delivery timeout (5s default), removal на timeout
6. RuntimePipe: exponential backoff retry (3 attempts, 500ms base delay)

**Фаза 3 — Observability (P2):**
7. CorrelationId: ICorrelatedSideEffect interface, TransactionId propagation в DurableQueueSideEffect, activity tags в SideEffectsWorker
8. AdaptiveInterval: utility class для всех трёх клиентов (success: 10s→60s, failure: 1s exponential backoff, jitter ±20%)
9. Dead letter: move to side_effects_dead_letter вместо delete, error message tracking, SideEffectDeadLetter metric
10. Dashboard: DeadLetterCount в SideEffectsStats/GetStats, 4-column grid с Dead Letter card в SideEffectsMonitor.razor

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `Infrastructure/Messaging/Channels/RuntimeChannel.cs` | SequencedMessage, CatchUpResult, ring buffer, CatchUp(), delivery timeout в SendSafe |
| `Infrastructure/Messaging/Channels/RuntimeChannelClient.cs` | CatchUp в Resubscribe(), AdaptiveInterval |
| `Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs` | LastSeenSequence tracking, SequencedMessage unwrap |
| `Infrastructure/Messaging/Channels/RuntimeChannelOptions.cs` | CatchUpBufferSize, DeliveryTimeoutSeconds |
| `Infrastructure/Messaging/Pipes/MessagePipeClient.cs` | Retry с backoff в Send(), IRuntimePipeConfig injection, AdaptiveInterval |
| `Infrastructure/Messaging/Pipes/RuntimePipeOptions.cs` | SendRetryCount, SendRetryBaseDelayMs |
| `Infrastructure/Messaging/Queues/DurableQueue.cs` | NoSubscribersException, проверки в Push() |
| `Infrastructure/Messaging/Queues/DurableQueueClient.cs` | CorrelationId в PushTransactional/PushDirect, AdaptiveInterval |
| `Infrastructure/Messaging/Queues/DurableQueueSideEffect.cs` | ICorrelatedSideEffect, CorrelationId [Id(2)] |
| `Infrastructure/Messaging/AdaptiveInterval.cs` | **Новый файл** — adaptive resubscribe utility |
| `Infrastructure/Data/Collections/StateCollection.cs` | UpdatedAt в StateCollectionUpdate, _lastUpdated tracking, conditional apply |
| `Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` | RequeueStuckOlderThan(), dead letter вместо delete, DeadLetterCount в stats |
| `Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | Periodic stuck check, CorrelationId в activity tags, error message в FailProcessing |
| `Infrastructure/Orleans/SideEffects/SideEffectsOptions.cs` | StuckCheckIntervalSeconds, StuckThresholdMinutes |
| `Common/Extensions/Metrics/BackendMetrics.cs` | 7 новых метрик: CatchUp (3), DeliveryTimeout, NoSubscribers, PipeRetry, DeadLetter |
| `Console/Pages/SideEffects/SideEffectsMonitor.razor` | Dead Letter card в 4-column grid |

### Проблемы и решения
1. **CS0136 variable name conflict**: `tx` в FailProcessing конфликтовал с existing scope → переименован в `deadLetterTx`
2. **CS4014 unawaited task**: `ObserverSource.Send(msg)` в catch-up replay → добавлен `await`
3. **Logic bug в DurableQueue**: Проверка "all observers failed" использовала некорректную формулу → заменена на `_observers.Count == 0` после removal
4. **RuntimeChannel.cs file drift**: Файл был обновлён (добавлен tracing) между анализом и реализацией → адаптировано

### Нерешенные вопросы
- SQL migration для `side_effects_dead_letter` таблицы — нужно создать перед деплоем
- [AlwaysInterleave] concurrency в ring buffer — безопасно для текущей нагрузки, мониторить при масштабировании
