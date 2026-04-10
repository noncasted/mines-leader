# Messaging Infrastructure Roadmap

## Цель

Довести in-cluster messaging до production-ready уровня: гарантированная доставка, ordering, observability.

## Текущая архитектура

Три примитива:

| Примитив | Паттерн | Durability | Файлы |
|---|---|---|---|
| **RuntimeChannel** | Pub/Sub 1:N | In-memory | `Messaging/Channels/` |
| **RuntimePipe** | Request/Response 1:1 | In-memory | `Messaging/Pipes/` |
| **DurableQueue** | Pub/Sub 1:N | Postgres (side effects) | `Messaging/Queues/` |

Транзакционная интеграция: `DurableQueueSideEffect` записывается в `side_effects_queue` атомарно в рамках Postgres-транзакции. `SideEffectsWorker` polling-ом достаёт и выполняет.

Все три примитива используют `IGrainObserver` для доставки + resubscribe loop каждые 10 секунд.

---

## Фаза 1: Надёжность доставки (P0)

Закрывает главную проблему: observer references теряются молча, сообщения пропадают без следа.

| # | Задача | Файл задачи | Оценка |
|---|--------|-------------|--------|
| 1 | Sequence numbers + ring buffer + catch-up в RuntimeChannel | [01_channel_sequence_catchup.md](01_channel_sequence_catchup.md) | 3-5 дней |
| 2 | DurableQueue.Push throw при 0 subscribers | [02_durable_queue_no_subscribers.md](02_durable_queue_no_subscribers.md) | 0.5 дня |
| 3 | Периодический RequeueStuck в SideEffectsWorker | [03_periodic_requeue_stuck.md](03_periodic_requeue_stuck.md) | 0.5 дня |

**Критерий завершения фазы:** При restart любого silo ни одно сообщение не теряется; DurableQueue гарантирует доставку при наличии хотя бы одного подписчика; застрявшие side effects автоматически возвращаются в очередь.

---

## Фаза 2: Ordering и consistency (P1)

Закрывает проблемы порядка доставки и отказоустойчивости cross-service RPC.

| # | Задача | Файл задачи | Оценка |
|---|--------|-------------|--------|
| 4 | Version-based idempotency в StateCollection | [04_statecollection_idempotency.md](04_statecollection_idempotency.md) | 2-3 дня |
| 5 | Timeout на Channel observer delivery | [05_channel_observer_timeout.md](05_channel_observer_timeout.md) | 1 день |
| 6 | Retry с backoff для RuntimePipe caller | [06_pipe_retry_backoff.md](06_pipe_retry_backoff.md) | 1 день |

**Критерий завершения фазы:** StateCollection применяет только более новые версии state; медленные observers не блокируют broadcast; RuntimePipe caller переживает кратковременные сбои handler'а.

---

## Фаза 3: Observability и операционность (P2)

Даёт visibility в поведение messaging в production и автоматизирует recovery.

| # | Задача | Файл задачи | Оценка |
|---|--------|-------------|--------|
| 7 | Distributed tracing: TransactionId propagation | [07_distributed_tracing.md](07_distributed_tracing.md) | 2-3 дня |
| 8 | Adaptive resubscribe interval | [08_adaptive_resubscribe.md](08_adaptive_resubscribe.md) | 1 день |
| 9 | Dead letter queue с alerting | [09_dead_letter_alerting.md](09_dead_letter_alerting.md) | 1 день |
| 10 | Messaging health dashboard | [10_messaging_dashboard.md](10_messaging_dashboard.md) | 1 день |

**Критерий завершения фазы:** Можно проследить путь сообщения от транзакции до consumer'а; resubscribe не создаёт лишнюю нагрузку; failed messages попадают в dead letter с alert'ом; есть единый dashboard для мониторинга.

---

## Зависимости между задачами

```
Фаза 1 (параллельно):
  [1] Channel catch-up  ──┐
  [2] DurableQueue throw  ├── все независимы
  [3] RequeueStuck        ──┘

Фаза 2 (после Фазы 1):
  [4] StateCollection idempotency  ── зависит от [1] (sequence numbers)
  [5] Channel timeout              ── независима
  [6] Pipe retry                   ── независима

Фаза 3 (после Фазы 2):
  [7] Tracing         ── независима
  [8] Adaptive resub  ── независима
  [9] Dead letter     ── зависит от [2] (DurableQueue error handling)
  [10] Dashboard      ── зависит от [7] (tracing metrics)
```

## Ключевые файлы инфраструктуры

| Файл | Роль |
|------|------|
| `Infrastructure/Messaging/Messaging.cs` | Фасад: DurableQueue + RuntimePipe + RuntimeChannel |
| `Infrastructure/Messaging/MessagingExtensions.cs` | Extension methods, DI registration |
| `Infrastructure/Messaging/Channels/RuntimeChannel.cs` | Pub/Sub grain + observers |
| `Infrastructure/Messaging/Channels/RuntimeChannelClient.cs` | Client-side: create consumers, resubscribe loop |
| `Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs` | IGrainObserver impl |
| `Infrastructure/Messaging/Pipes/MessagePipe.cs` | Request/Response grain |
| `Infrastructure/Messaging/Pipes/MessagePipeClient.cs` | Client-side: add handlers, send requests |
| `Infrastructure/Messaging/Queues/DurableQueue.cs` | Persistent pub/sub grain |
| `Infrastructure/Messaging/Queues/DurableQueueClient.cs` | Client-side: transactional + direct push |
| `Infrastructure/Messaging/Queues/DurableQueueSideEffect.cs` | Side effect bridge |
| `Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | Polling worker, executes side effects |
| `Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` | Postgres storage: queue/processing/retry tables |
| `Infrastructure/Orleans/Transactions/Transactions.cs` | Transaction coordinator |
| `Infrastructure/Data/Collections/StateCollection.cs` | In-memory dict synced via DurableQueue |
