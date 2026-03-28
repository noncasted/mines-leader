## Задача: Рефакторинг системы мессаджинга: DurableQueue / RuntimePipe / RuntimeChannel

### Цель
Разделить `MessageQueue` и `MessagePipe` на три отдельных примитива с чёткими контрактами: `DurableQueue` (персистентная очередь с транзакциями), `RuntimePipe` (только request-response туннель), `RuntimeChannel` (runtime-only broadcast для нескольких слушателей), и перевести ServiceDiscovery/AddressableState/DynamicState/UserProjection/CoordinatorEvents на `RuntimeChannel`.

---

### Шаги реализации

**Блок 1 — создать RuntimeChannel инфраструктуру**

1. Создать `IRuntimeChannelId`, `IRuntimeChannel` (Orleans grain), `RuntimeChannel` — `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs` [новый файл]
2. Создать `IRuntimeChannelObserver` — `backend/Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs` [новый файл]
3. Создать `IRuntimeChannelClient`, `RuntimeChannelClient` — `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs` [новый файл]

`IRuntimeChannel` — аналог `IMessageQueue`, но `Push()` вызывается напрямую через Orleans, без `ISideEffectsStorage`. `IRuntimeChannelClient` — аналог `MessageQueueClient`, без `PushTransactional` и без `ISideEffectsStorage` в конструкторе.

**Блок 2 — переименовать MessageQueue → DurableQueue**

4. Переименовать классы/интерфейсы: `IMessageQueue` → `IDurableQueue`, `MessageQueue` → `DurableQueue`, `IMessageQueueClient` → `IDurableQueueClient`, `MessageQueueClient` → `DurableQueueClient`, `IMessageQueueObserver` → `IDurableQueueObserver`, `MessageQueueObserver` → `DurableQueueObserver`, `MessageQueueOptions` → `DurableQueueOptions`, `MessageQueueSideEffect` → `DurableQueueSideEffect` — файлы в `backend/Infrastructure/Messaging/Queues/`

**Блок 3 — переименовать MessagePipe → RuntimePipe + удалить одностороннюю отправку**

5. Переименовать `IMessagePipe` → `IRuntimePipe`, `MessagePipe` → `RuntimePipe`; удалить метод `Task Send(object message)` из интерфейса и реализации — `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs`
6. Переименовать `IMessagePipeClient` → `IRuntimePipeClient`, `MessagePipeClient` → `RuntimePipeClient`; удалить метод `Task Send(IMessagePipeId id, object message)` — `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs`
7. Переименовать `IMessagePipeObserver` → `IRuntimePipeObserver`, `MessagePipeObserver` → `RuntimePipeObserver` — `backend/Infrastructure/Messaging/Pipes/MessagePipeObserver.cs`

**Блок 4 — обновить IMessaging, расширения и ID-типы**

8. Обновить `IMessaging` и `Messaging`: `IMessageQueueClient Queue` → `IDurableQueueClient DurableQueue`, `IMessagePipeClient Pipe` → `IRuntimePipeClient RuntimePipe`, добавить `IRuntimeChannelClient RuntimeChannel`; обновить `AddMessaging()` — `backend/Infrastructure/Messaging/Messaging.cs`
9. Обновить `MessagingExtensions.cs`: переименовать `IMessageQueueId` → `IDurableQueueId`, `MessageQueueId` → `DurableQueueId`, `IMessagePipeId` → `IRuntimePipeId`, `MessagePipeId` → `RuntimePipeId`; добавить `IRuntimeChannelId`, `RuntimeChannelId`; убрать extension `SendPipe(id, object)` (без `TResponse`); добавить `ListenChannel<T>`, `PublishChannel` — `backend/Infrastructure/Messaging/MessagingExtensions.cs`

**Блок 5 — перевести пользователей MessageQueue (runtime-only) на RuntimeChannel**

10. `ServiceDiscovery.cs`: `IMessageQueueId` → `IRuntimeChannelId`, `ListenQueue` → `ListenChannel`, `PushDirectQueue` → `PublishChannel` — `backend/Cluster/Discovery/ServiceDiscovery.cs`
11. `AddressableState.cs`: то же самое, `AddressableStateMessageQueueId<T>` → `AddressableStateChannelId<T>` — `backend/Infrastructure/Data/State/AddressableState.cs`
12. `DynamicState.cs`: то же самое, `DynamicStateMessageQueueId<T>` → `DynamicStateChannelId<T>` — `backend/Infrastructure/Data/State/DynamicState.cs`

**Блок 6 — перевести UserProjection на RuntimeChannel**

13. `UserProjection.cs`: `UserProjectionPipeId : IMessagePipeId` → `UserProjectionChannelId : IRuntimeChannelId`, `_messaging.SendPipe(_pipeId, payload)` → `_messaging.PublishChannel(_channelId, payload)` — `backend/Meta/Users/Projections/UserProjection.cs`
14. `UserConnectionEntryPoint.cs`: `ListenPipe<IProjectionPayload>` → `ListenChannel<IProjectionPayload>` — `backend/Orchestration/MetaGateway/UserFlow/UserConnectionEntryPoint.cs`

**Блок 7 — перевести CoordinatorEvents на RuntimeChannel**

15. `CoordinatorEvents.cs`: `ReadyPipeId : IMessageQueueId` → `ReadyChannelId : IRuntimeChannelId` — `backend/Cluster/Coordination/CoordinatorEvents.cs`
16. `ClusterParticipantStartup.cs`: `ListenQueue` → `ListenChannel` — `backend/Cluster/Coordination/ClusterParticipantStartup.cs`
17. `ClusterCoordinator.cs`: `PushDirectQueue` → `PublishChannel` — `backend/Orchestration/Coordinator/ClusterCoordinator.cs`

**Блок 8 — обновить StateCollection (остаётся на DurableQueue)**

18. Переименовать `StateCollectionMessageQueueId<TKey, TValue>` → `StateCollectionDurableQueueId<TKey, TValue>`; обновить вызовы на новые имена — `backend/Infrastructure/Data/Collections/StateCollection.cs`

**Блок 9 — обновить тестовую инфраструктуру**

19. Переименовать `PipeId : IMessagePipeId` → `PipeId : IRuntimePipeId` в `ClusterTestNodeMessages.cs` и обновить `ClusterTestUtils.cs` — `backend/Tests/Common/ClusterTestNodeMessages.cs`, `backend/Tests/Common/ClusterTestUtils.cs`
20. Обновить `MessagePipeSendStressTest.cs`: заменить `ListenPipe`/`SendPipe` на `ListenChannel`/`PublishChannel`, переименовать на `RuntimeChannelSendStressTest` — `backend/Tests/Messaging/MessagePipeSendStressTest.cs`
21. Переименовать `MessagingDirectQueueStressTest` → `DurableQueueDirectStressTest`, `MessagingTransactionalQueueStressTest` → `DurableQueueTransactionalStressTest` — файлы в `backend/Tests/Messaging/`

**Блок 10 — добавить тест для RuntimeChannel**

22. Создать `RuntimeChannelStressTest.cs` по образцу `MessagingDirectQueueStressTest`: 5 нод публикуют в канал, root слушает, проверяет доставку всех сообщений — `backend/Tests/Messaging/RuntimeChannelStressTest.cs` [новый файл]

---

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Infrastructure/Messaging/Messaging.cs` | IMessaging — добавить `RuntimeChannel`, переименовать `Queue`/`Pipe` |
| `backend/Infrastructure/Messaging/MessagingExtensions.cs` | Все ID-типы и extension-методы — полная замена имён |
| `backend/Infrastructure/Messaging/Queues/MessageQueueClient.cs` | Переименовать в `DurableQueueClient`, он использует `ISideEffectsStorage` |
| `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs` | Переименовать в `RuntimePipeClient`, убрать одностороннюю отправку |
| `backend/Infrastructure/Data/Collections/StateCollection.cs` | Остаётся на DurableQueue — использует `PushTransactional` |
| `backend/Meta/Users/Projections/UserProjection.cs` | Переходит с Pipe на RuntimeChannel |
| `backend/Orchestration/MetaGateway/UserFlow/UserConnectionEntryPoint.cs` | Слушает ProjectionPayload — переходит на ListenChannel |
| `backend/Cluster/Discovery/ServiceDiscovery.cs` | Переходит с MessageQueue на RuntimeChannel |
| `backend/Tests/Common/ClusterTestUtils.cs` | Использует request-response pipe — только переименование типов |

---

### Документация к прочтению
- `rules/ORLEANS_GRAINS.md` — паттерн Orleans grain для нового `RuntimeChannel` grain

---

### Риски
- `UserProjection.SendPipe` вызывается из гейтвея через `SendPipe` без `TResponse` — этот метод удаляется из `IMessagePipeClient`; нужно убедиться что все подобные вызовы перенесены на `RuntimeChannel` до удаления метода
- `ClusterTestUtils` использует `SendPipe<TResponse>` (с возвратом) — этот метод остаётся на RuntimePipe и не удаляется
- `MessagePipeSendStressTest` тестирует именно одностороннюю отправку через Pipe — тест нужно конвертировать, иначе после удаления метода `Send(object)` тест перестанет компилироваться
- SDK-style csproj (оба проекта — `Infrastructure.csproj` и `Tests.csproj`) автоматически подхватывают новые `.cs` файлы — ручное добавление в `.csproj` не требуется
