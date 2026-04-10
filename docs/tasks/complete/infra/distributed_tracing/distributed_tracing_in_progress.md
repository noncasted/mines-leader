## Distributed Tracing — Рабочие заметки

### Статус: Завершено

### Выполненные шаги

**1. TraceExtensions.cs** — добавлены 6 новых ActivitySource:
- `Infrastructure.Transactions`
- `Infrastructure.SideEffects`
- `Infrastructure.Messaging.DurableQueue`
- `Infrastructure.Messaging.RuntimePipe`
- `Infrastructure.Messaging.RuntimeChannel`
- `Infrastructure.TaskBalancer`
- Все добавлены в `AllSources` (автоматически подхватываются OTEL pipeline)

**2. Transactions.cs** — инструментирован метод `Process`:
- Span `Transaction.Process` с тегами `transaction.id`, `participant.count`
- `Rollback()` принимает `Activity?` и устанавливает `ActivityStatusCode.Error`

**3. SideEffectsWorker.cs** — инструментирован метод `ExecuteEntry`:
- Span `SideEffect.Execute` с тегами `side_effect.id`, `side_effect.retry_count`

**4. DurableQueue.cs** — инструментирован метод `Push`:
- Span `DurableQueue.Push` с тегами `message.type`, `observer.count`

**5. MessagePipe.cs (RuntimePipe)** — инструментирован метод `Send`:
- Span `RuntimePipe.Send` с тегами `message.type`, `pipe.timeout`
- `SetStatus(Error)` в catch блоках TimeoutException и Exception

**6. RuntimeChannel.cs** — инструментирован метод `Publish`:
- Span `RuntimeChannel.Publish` с тегами `message.type`, `observer.count`

**7. TaskBalancer.cs** — инструментирован локальный метод `Execute` в `ExecuteLoop`:
- Span `TaskBalancer.Execute` с тегами `task.priority`, `task.score`
- `SetStatus(Error)` при исключении

### Решения
- Не добавлялись высококардинальные теги (grain ID, payload)
- `activity?.SetTag(...)` — nullsafe вызовы (sampling может не создать Activity)
- `using System.Diagnostics` добавлен во все изменённые файлы
