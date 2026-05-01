## Messaging reliability fixes — Результат

### Статус: Завершено

### Что сделано
- `DurableQueue` больше не ack-ает delivery, если ни один observer/local handler не обработал сообщение успешно; failures удаляют stale observers и оставляют side effect в retry path.
- `DurableQueueClient` и `RuntimeChannelClient` переведены с shared `ViewableDelegate` ownership на явные local handler tables: lifetime termination удаляет handler, а последний handler чистит Orleans object reference и observer в grain.
- Добавлены async listener overloads для durable queue и runtime channel; delivery loop теперь await-ит `Func<T, Task>` handlers, а sync `Action<T>` остается поддержанным wrapper-ом.
- `RuntimeChannel` получил reset/gap detection для `lastSeenSequence > currentSequence`, FIFO-by-grain-turn contract после удаления `[AlwaysInterleave]`, buffering live deliveries during catch-up и duplicate filtering by sequence.
- `RuntimePipe` получил observer identity, `UnbindObserver`, liveness `Ping()`, cleanup on lifetime termination и retry boundary: application handler failures are not retried, transport-style failures remain retryable.
- `SideEffectsStorage.Write(ISideEffect)` теперь rethrow-ит storage write failure; rollback failure логируется отдельно и не маскирует исходную ошибку.
- Добавлены targeted messaging regression tests и knowledge-base lesson про deadlock risk при удержании local delivery lock через Orleans observer binding/catch-up.

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Infrastructure/Messaging/Queues/DurableQueue.cs` | Подсчет successful/failed deliveries, no-success throw, observer cleanup, queue/correlation/observer logging tags. |
| `backend/Infrastructure/Messaging/Queues/DurableQueueClient.cs` | Local handler ref-counting, lifetime cleanup of transport observer/reference, async handler delivery, adaptive interval failure propagation, resubscribe snapshot race fix. |
| `backend/Infrastructure/Messaging/Queues/DurableQueueObserver.cs` | `Func<object, Task>` observer path with sync `Action<object>` compatibility. |
| `backend/Infrastructure/Messaging/Queues/DurableQueueSideEffect.cs` | Correlation id passed into `DurableQueue.Push`. |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs` | Removed `[AlwaysInterleave]`, documented publish ordering, reset/gap detection for stale `lastSeenSequence`, sequence/observer logging. |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs` | Local handler ref-counting, lifetime cleanup, async handler delivery, catch-up/live buffering flow, reset handling, adaptive interval failure propagation, resubscribe snapshot race fix. |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs` | Awaitable delivery, sequence duplicate filtering, catch-up replay, live buffering, `ResetLastSeen`. |
| `backend/Infrastructure/Messaging/MessagingExtensions.cs` | Clean listener API wrappers and async overloads for queue/channel listeners. |
| `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs` | Observer id binding/unbinding, liveness check, stale observer discard, application-vs-transport failure classification. |
| `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs` | Lifetime cleanup, duplicate handler cleanup, observer id registration, adaptive interval failure propagation, retry classification, resubscribe snapshot race fix. |
| `backend/Infrastructure/Messaging/Pipes/MessagePipeObserver.cs` | `Ping`, handler cleanup, application handler failure wrapping marker. |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` | Direct write now rethrows original storage failure and logs rollback failure separately. |
| `backend/Tools/Tests/Messaging/DurableQueueSubscriberTests.cs` | Regression test for all subscribers failing. |
| `backend/Tools/Tests/Messaging/DurableQueueTests.cs` | Tests for terminated listener no-ack and async durable listener awaiting; removed unreachable test code. |
| `backend/Tools/Tests/Messaging/RuntimeChannelCatchUpTests.cs` | Regression test for `lastSeenSequence > currentSequence` gap detection. |
| `backend/Tools/Tests/Messaging/RuntimeChannelTests.cs` | Tests for async listener awaiting, reset-lower-sequence delivery, and catch-up/live duplicate filtering. |
| `backend/Tools/Tests/Messaging/RuntimePipeRetryTests.cs` | Retry contract test changed to assert application handler failure is not retried. |
| `backend/Tools/Tests/Messaging/RuntimePipeTests.cs` | Test for `IsPipeExists == false` after handler lifetime termination. |
| `docs/db/docs/CLAUDE_MISTAKES.md` | Added lesson: do not hold local delivery locks across Orleans observer binding/catch-up. |

### Отличия от плана
- `RuntimeChannel` overlap fix uses local live-delivery buffering in `RuntimeChannelObserver` rather than a channel epoch/generation. This keeps the change smaller and avoids a protocol/envelope rewrite.
- `RuntimePipe` distinguishes application handler failures using a wrapped failure marker in the propagated exception message rather than a new generated exception type, to avoid introducing a new Orleans serialization surface in this pass.

### Проверка
- `dotnet test backend/Tools/Tests/Tests.csproj -- --filter-namespace "Tests.Messaging"` — Passed: 63, Failed: 0, Skipped: 0.
- `code-reviewer` pass after initial implementation found blocker/major issues; fixes were applied.
- Follow-up `code-reviewer` verification checked five prior findings and marked all as pass.
- `git diff --check` on touched messaging/test/docs files produced no whitespace errors.

### Нерешенные вопросы
- Existing dependency warnings remain: `OpenTelemetry.Api` / `OpenTelemetry.Exporter.OpenTelemetryProtocol` known moderate vulnerabilities (`NU1902`). They are pre-existing and outside this task.
- Existing xUnit analyzer warnings for `Task.Delay`/cancellation tokens remain in broader test project output. Messaging scoped tests still pass.
