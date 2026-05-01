## Задача: исправление надежности Messaging

### Цель
Исправить проблемы, найденные read-only аудитом подсистемы `backend/Infrastructure/Messaging`, без большого rewrite и с сохранением понятных delivery contracts.

Полный набор требований, преобразованный из аудита и запроса на `/workflow`:

1. Устранить silent loss в `DurableQueue`: сообщение не должно считаться успешно обработанным, если не было ни одной успешной доставки активному подписчику.
2. Исправить lifetime cleanup подписок для `DurableQueue` и `RuntimeChannel`: `Terminate()` должен удалять не только user callback, но и client listener, Orleans object reference и observer в grain, когда больше нет локальных подписчиков.
3. Исправить cleanup/liveness для `RuntimePipe`: после termination handler pipe не должен выглядеть существующим, а stale observer не должен заставлять callers ждать полный timeout/retry cycle.
4. Исправить adaptive resubscribe interval: failure path `AdaptiveInterval.RecordFailure()` должен реально использоваться при ошибках `AddObserver`/`BindObserver`.
5. Уточнить и стабилизировать `RuntimeChannel` catch-up semantics: gap после sequence reset/deactivation должен обнаруживаться, а overlap live delivery/catch-up не должен приводить к неявным дублям или пропускам.
6. Зафиксировать ordering semantics для `RuntimeChannel`: либо обеспечить FIFO per channel/observer, либо явно закрепить отсутствие guarantee и покрыть это тестами.
7. Пересмотреть retry policy `RuntimePipe`: handler exceptions и transport failures должны различаться, чтобы retry не дублировал non-idempotent side effects.
8. Исправить `PushDirectQueue`/`SideEffectsStorage.Write` failure surfacing: caller не должен получать успешный `Task`, если durable side effect не записан.
9. Закрыть API gap для async listeners: текущий `Action<T>` не должен маскировать `async void` поведение там, где delivery loop ожидает timeout/error semantics.
10. Усилить observability: добавить в ключевые логи/metrics/traces идентификаторы channel/queue/pipe, observer/subscriber, sequence, correlation/message id там, где они уже есть или вводятся.
11. Добавить targeted messaging tests перед или вместе с исправлениями и запускать только `Tests.Messaging`.
12. Не менять несвязанные подсистемы и не делать большой rewrite без отдельного решения.

### Контекст
Read-only аудит уже выполнен. Наблюденные факты:

- Scoped messaging tests проходили командой:
  `dotnet test backend/Tools/Tests/Tests.csproj -- --filter-namespace "Tests.Messaging"`
  Результат: `Passed: 55, Failed: 0, Skipped: 0`.
- LSP в проекте не настроен (`No language servers configured`), поэтому для поиска callsites использовать `grep`/`find`/`ast_grep`.
- Основные риски лежат на границе Orleans grain observers, side effects retry pipeline и custom `Lifetime` cleanup.
- Новые `.cs` файлы по проектному правилу должны добавляться в соответствующий `.csproj` вручную. В этом плане предпочтение — расширять существующие messaging test files, чтобы не плодить новые test files без необходимости.

### Шаги реализации

**1. Зафиксировать текущие contracts тестами до изменения поведения**
  1.1. Добавить regression test: `DurableQueue.Push` должен возвращать failure/retry, если все observers упали во время delivery — `backend/Tools/Tests/Messaging/DurableQueueSubscriberTests.cs`.
  1.2. Добавить regression test: terminated durable listener не должен silently ack-ать message — `backend/Tools/Tests/Messaging/DurableQueueTests.cs`.
  1.3. Добавить test: после termination pipe handler `IsPipeExists` должен возвращать false или explicit stale status — `backend/Tools/Tests/Messaging/RuntimePipeTests.cs`.
  1.4. Добавить test: `RuntimeChannel.CatchUp(lastSeenSequence > currentSequence)` должен возвращать `GapDetected = true` — `backend/Tools/Tests/Messaging/RuntimeChannelCatchUpTests.cs`.
  1.5. Добавить test: `RuntimePipe` не должен retry-ить non-transient handler exception, если выбран такой contract — `backend/Tools/Tests/Messaging/RuntimePipeRetryTests.cs`.
  1.6. Добавить test для real async listener semantics только после выбора API формы (`Func<T, Task>` overload или запрет async `Action<T>`) — `backend/Tools/Tests/Messaging/RuntimeChannelTests.cs` и/или `backend/Tools/Tests/Messaging/DurableQueueTests.cs`.

**2. Исправить DurableQueue delivery acknowledgement**
  2.1. Ввести подсчет успешных доставок в `Push(object message)` — `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`.
  2.2. Если `_observers.Count == 0` на входе — сохранить текущее throw/retry поведение — `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`.
  2.3. Если observers были, но `successCount == 0` после delivery failures — бросать exception после cleanup, чтобы side effect остался в retry pipeline — `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`.
  2.4. Проверить interaction с `SideEffectsWorker.ExecuteEntry` и `FailProcessing` — `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`, `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs`.
  2.5. Добавить лог с queue id, observer count, failed count, success count — `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`.

**3. Исправить lifetime cleanup для DurableQueue и RuntimeChannel**
  3.1. Спроектировать local subscription accounting: один transport observer per raw id, несколько local callbacks с reference count — `backend/Infrastructure/Messaging/Queues/DurableQueueClient.cs`, `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs`.
  3.2. На `lifetime.Terminate()` удалять конкретный callback; при последнем callback вызывать full cleanup: `DeleteObjectReference`, `RemoveObserver`, remove from `_listeners` — `backend/Infrastructure/Messaging/Queues/DurableQueueClient.cs`, `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs`.
  3.3. Сделать cleanup idempotent и безопасным при повторной подписке — `backend/Infrastructure/Messaging/Queues/DurableQueueClient.cs`, `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs`.
  3.4. Проверить, что `RemoveConsumer` либо становится частью interface, либо удаляется/заменяется внутренним механизмом — `backend/Infrastructure/Messaging/Queues/DurableQueueClient.cs`, `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs`.
  3.5. Не менять `Common.Reactive.EventSource` глобально без отдельной причины; исправлять ownership на уровне messaging clients — `backend/Common/Reactive/Events/EventSource.cs`.

**4. Исправить RuntimePipe cleanup и liveness**
  4.1. Добавить observer identity/generation в pipe binding — `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs`, `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs`.
  4.2. Добавить grain API для unbind only-if-same observer/generation — `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs`.
  4.3. На lifetime cleanup вызывать unbind и удалять object reference/local listener — `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs`.
  4.4. Обновить `Exists`/`HasObserver` semantics так, чтобы stale observer не считался здоровым handler — `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs`, `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs`.
  4.5. Проверить callers, которые полагаются на `IsPipeExists`, особенно deploy id acquisition — `backend/Cluster/Coordination/ClusterParticipantStartup.cs`, `backend/Orchestration/Coordinator/DeployIdentity.cs`.

**5. Исправить AdaptiveInterval integration**
  5.1. Изменить `Listener.Resubscribe()` для queues/channels/pipes: возвращать `bool` success/failure или пробрасывать exception — `backend/Infrastructure/Messaging/Queues/DurableQueueClient.cs`, `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs`, `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs`.
  5.2. Сконцентрировать logging и `RecordSuccess`/`RecordFailure` в одном уровне, чтобы состояние interval соответствовало реальности — те же файлы.
  5.3. Добавить unit tests, которые проверяют failure path без ожидания реального времени — `backend/Tools/Tests/Messaging/AdaptiveIntervalTests.cs` или existing client-level tests.

**6. Уточнить RuntimeChannel catch-up/order semantics**
  6.1. При `lastSeenSequence > _sequenceNumber` возвращать gap или ввести channel epoch/generation — `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs`.
  6.2. Защитить от duplicate delivery при catch-up/live overlap: фильтровать stale `SequencedMessage` в `RuntimeChannelObserver.Send` или сериализовать catch-up относительно live delivery — `backend/Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs`, `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs`.
  6.3. Принять решение по `[AlwaysInterleave]` на `Publish` и `CatchUp`: если FIFO важен, убрать/ограничить interleaving; если throughput важнее, явно документировать unordered concurrent publish — `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs`.
  6.4. Обновить tests: заменить `BeEquivalentTo` там, где должен проверяться порядок, либо явно назвать unordered contract — `backend/Tools/Tests/Messaging/RuntimeChannelTests.cs`.

**7. Пересмотреть RuntimePipe retry contract**
  7.1. Разделить transport failures и handler failures. Минимальный вариант: не retry-ить application exceptions, retry-ить только timeout/broken observer/transient Orleans exceptions — `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs`, `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs`, `backend/Infrastructure/Messaging/Pipes/MessagePipeObserver.cs`.
  7.2. Если retry handler failures нужен, ввести explicit opt-in или idempotency key/request id — `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs`, `backend/Infrastructure/Messaging/Pipes/RuntimePipeOptions.cs`.
  7.3. Обновить `RuntimePipeRetryTests`: generic handler exception больше не должен автоматически считаться transient, если выбран безопасный default — `backend/Tools/Tests/Messaging/RuntimePipeRetryTests.cs`.

**8. Исправить failure surfacing для PushDirectQueue**
  8.1. Изменить `SideEffectsStorage.Write(ISideEffect)` так, чтобы storage failure пробрасывался caller-у или возвращал explicit result — `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs`.
  8.2. Проверить все callsites `Write(ISideEffect)`, чтобы не сломать ожидаемое graceful поведение — `backend/Infrastructure/Orleans/SideEffects/*`, `backend/Infrastructure/Messaging/Queues/DurableQueueClient.cs`.
  8.3. Добавить тест на failure surfacing через fake/substitute storage, если DI позволяет, или integration-level DB failure test — `backend/Tools/Tests/Messaging/DurableQueueTests.cs`.

**9. Исправить async listener API без резкого rewrite**
  9.1. Добавить overloads `ListenDurableQueue<T>(..., Func<T, Task> listener)` и `ListenChannel<T>(..., Func<T, Task> listener)` — `backend/Infrastructure/Messaging/MessagingExtensions.cs`.
  9.2. Добавить async-capable observer path, который await-ит handler и возвращает failure в delivery loop — `backend/Infrastructure/Messaging/Queues/DurableQueueObserver.cs`, `backend/Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs`.
  9.3. Оставить sync `Action<T>` overload для существующих callsites, но не использовать `async` lambda с ним — audit current callsites and benchmarks.
  9.4. Исправить misleading benchmark slow listener — `backend/Tools/Benchmarks/Messaging/RuntimeChannelDeliveryTimeoutTest.cs`.

**10. Усилить observability и documentation of contracts**
  10.1. Добавить tags/log fields: queue/channel/pipe raw id, observer id/generation, sequence, success/failure counts, correlation id — `backend/Infrastructure/Messaging/**/*`.
  10.2. Протянуть existing `DurableQueueSideEffect.CorrelationId` в queue logs/activity, если возможно без envelope rewrite — `backend/Infrastructure/Messaging/Queues/DurableQueueSideEffect.cs`, `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`.
  10.3. Добавить краткий contract comment/XML docs для public interfaces — `backend/Infrastructure/Messaging/Messaging.cs`, `backend/Infrastructure/Messaging/MessagingExtensions.cs`, concrete interfaces in `Channels`, `Pipes`, `Queues`.
  10.4. Если в ходе исправлений выявится повторяемая ошибка AI/разработчиков, обновить knowledge base согласно проектному правилу — `docs/db/docs/CLAUDE_MISTAKES.md` или релевантный docs file.

**11. Verification**
  11.1. После каждого блока запускать scoped tests:
       `dotnet test backend/Tools/Tests/Tests.csproj -- --filter-namespace "Tests.Messaging"`.
  11.2. Для изменений в side effects, если messaging tests не покрывают regressions, запускать только добавленные/измененные tests через `--filter-class` или `--filter-method` — `backend/Tools/Tests/Tests.csproj`.
  11.3. Если появляются UTF-16LE logs, читать их через `tools/scripts/get-test-log.sh` согласно `docs/db/docs/TESTING.md`.
  11.4. Проверить `git diff` только по ожидаемым files; не трогать unrelated текущие изменения в workspace.

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Infrastructure/Messaging/Queues/DurableQueue.cs` | Delivery acknowledgement и observer failure semantics для durable queue. |
| `backend/Infrastructure/Messaging/Queues/DurableQueueClient.cs` | Client-side subscription lifetime, object reference cleanup, transactional/direct queue API. |
| `backend/Infrastructure/Messaging/Queues/DurableQueueObserver.cs` | Sync/async observer callback semantics. |
| `backend/Infrastructure/Messaging/Queues/DurableQueueSideEffect.cs` | Durable side effect execution and correlation id. |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs` | Runtime publish, sequencing, catch-up ring buffer, ordering/interleaving. |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs` | Channel resubscribe loop, catch-up invocation, local consumer lifecycle. |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs` | `LastSeenSequence`, duplicate/stale message handling, sync/async callback invocation. |
| `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs` | Pipe observer binding, liveness, timeout, reentrancy. |
| `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs` | Pipe retry policy, handler lifecycle, resubscribe interval integration. |
| `backend/Infrastructure/Messaging/Pipes/MessagePipeObserver.cs` | Handler exception/type mismatch propagation. |
| `backend/Infrastructure/Messaging/AdaptiveInterval.cs` | Backoff algorithm; should remain small, but integration must be corrected. |
| `backend/Infrastructure/Messaging/MessagingExtensions.cs` | Public API surface for queue/channel/pipe operations and future async listener overloads. |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | DurableQueue side effects retry/complete semantics. |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` | Direct side effect write failure behavior and retry/deadletter state. |
| `backend/Tools/Tests/Messaging/DurableQueueTests.cs` | Existing durable queue integration tests; extend for cleanup and storage/delivery semantics. |
| `backend/Tools/Tests/Messaging/DurableQueueSubscriberTests.cs` | Existing direct grain subscriber failure tests; extend for all-observers-fail behavior. |
| `backend/Tools/Tests/Messaging/RuntimeChannelTests.cs` | RuntimeChannel delivery/order/lifetime tests. |
| `backend/Tools/Tests/Messaging/RuntimeChannelCatchUpTests.cs` | Catch-up and sequence/gap tests. |
| `backend/Tools/Tests/Messaging/RuntimePipeTests.cs` | Pipe lifecycle/liveness/request-response tests. |
| `backend/Tools/Tests/Messaging/RuntimePipeRetryTests.cs` | Retry policy tests. |
| `backend/Tools/Benchmarks/Messaging/RuntimeChannelDeliveryTimeoutTest.cs` | Benchmark currently uses async `Action`; should be corrected after async listener API decision. |
| `backend/Tools/Tests/Tests.csproj` | Test project for scoped messaging verification; avoid new files if possible, otherwise add manually per repo rule. |

### Документация к прочтению

- `docs/db/docs/COMMON_ORLEANS.md` — Orleans grains, observer/runtime semantics, state/transaction patterns.
- `docs/db/docs/COMMON_LIFETIMES.md` — `Lifetime`, `Advise`, subscription cleanup, terminate behavior.
- `docs/db/docs/API_DESIGN_FULL.md` — async API shape, return types, surfacing failures.
- `docs/db/docs/TESTING.md` — xUnit v3 filter syntax and UTF-16LE log reading workflow.
- `docs/db/docs/CODE_STYLE_FULL.md` — member order/naming/`NoAwait` conventions before editing infrastructure code.
- `docs/db/docs/COMMON_REACTIVE_BASICS.md` — `EventSource`/`ViewableDelegate` behavior when changing subscription accounting.

### Риски

- **Behavioral compatibility:** `DurableQueue` currently intentionally drops messages pushed with no active listener after retries/deadletter timing. Fixing all-observers-fail must not accidentally replay old messages to late subscribers unless contract changes explicitly.
- **Retry amplification:** changing `RuntimePipe` retry policy can affect startup/deploy id acquisition and session request flows; callsites must be checked.
- **Orleans interleaving:** removing `[AlwaysInterleave]` may reduce throughput; keeping it may preserve out-of-order risk. This is an explicit design decision.
- **Async listener migration:** adding `Func<T, Task>` overloads without updating callsites can leave old `async Action` bugs in benchmarks or production.
- **Existing workspace changes:** repo already has unrelated modified files. Implementation must only touch planned files and must not clean/reset unrelated work.
- **Test timing:** current messaging tests use real delays in places. Prefer deterministic hooks/fakes over longer sleeps.
