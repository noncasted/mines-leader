## Distributed Tracing — Результат

### Статус: Завершено

### Что сделано
- Добавлены 6 ActivitySource в TraceExtensions (Transactions, SideEffects, Messaging x3, TaskBalancer)
- Spans в Transactions.Process() и Rollback() с тегами transaction.id, participant.count
- Span в SideEffectsWorker.ExecuteEntry() с retry_count, correlation_id
- Spans в DurableQueue.Push(), RuntimePipe.Send(), RuntimeChannel.Publish()
- Span в TaskBalancer.Execute() с task.priority, task.score
- Error status на всех catch-блоках

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Common/Extensions/Traces/TraceExtensions.cs` | +6 ActivitySource, обновлен AllSources |
| `backend/Infrastructure/Orleans/Transactions/Transactions.cs` | Span в Process(), Error в Rollback() |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | Span в ExecuteEntry() |
| `backend/Infrastructure/Messaging/Queues/DurableQueue.cs` | Span в Push() |
| `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs` | Span в Send(), Error в catch |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs` | Span в Publish() |
| `backend/Infrastructure/Execution/TaskScheduling/TaskBalancer.cs` | Span в Execute(), Error в catch |

### Отличия от плана
- Шаг 2.3 (пробросить Activity.Current через TransactionContext) — пропущен: correlation уже обеспечивается через AsyncLocal Activity.Current, явный проброс избыточен
- Шаг 3.2 (span на каждый цикл Loop в SideEffectsWorker) — пропущен: loop-level span избыточен при наличии per-entry spans, создаёт шум в трейсах
- Тег `side_effect.id` из плана заменён на `side_effect.correlation_id` — id был high-cardinality (уникальный Guid), убран при review
- Воркер добавил catch-up систему в RuntimeChannel (SequencedMessage, кольцевой буфер, CatchUp метод) — выходит за scope задачи
- Добавлен NoSubscribersException в DurableQueue — breaking change, исправлен при review
- Добавлены дополнительные метрики (delivery timeout, catch-up, pipe retry) сверх плана

### Исправления после review
- CRITICAL: RuntimeChannel.Publish() отправлял SequencedMessage wrapper в observer — исправлено unwrapping на стороне RuntimeChannelObserver.Send()
- CRITICAL: DurableQueue.Push() бросал NoSubscribersException — заменён на warning log
- MEDIUM: Убран high-cardinality tag side_effect.id
- MEDIUM: Убран неиспользуемый параметр observerId из CatchUp
