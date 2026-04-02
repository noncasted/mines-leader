# Messaging

Три паттерна обмена сообщениями, агрегированные в `IMessaging`.

## Обзор паттернов

```mermaid
graph TB
    subgraph DQ["DurableQueue (надёжная доставка)"]
        DQP[Producer] -->|Push| DQG[DurableQueue Grain]
        DQG -->|Deliver| DQC1[Consumer 1]
        DQG -->|Deliver| DQC2[Consumer 2]
    end

    subgraph RP["RuntimePipe (запрос-ответ)"]
        RPC[Caller] -->|Send + Wait| RPG[RuntimePipe Grain]
        RPG -->|Forward| RPH[Handler]
        RPH -->|Response| RPG
        RPG -->|Response| RPC
    end

    subgraph RC["RuntimeChannel (pub/sub)"]
        RCP[Publisher] -->|Publish| RCG[RuntimeChannel Grain]
        RCG -->|Broadcast| RCS1[Subscriber 1]
        RCG -->|Broadcast| RCS2[Subscriber 2]
    end
```

---

## DurableQueue

**Назначение:** Надёжная доставка сообщений для синхронизации `StateCollection`.

| Характеристика | Значение |
|---------------|----------|
| Тип грейна | `IGrainWithStringKey` |
| Гарантия доставки | At-least-once |
| Подписчики | Множественные (multi-observer) |
| Deactivation | Auto-timeout |

### Как работает

```mermaid
sequenceDiagram
    participant P as Producer (Grain)
    participant Q as DurableQueue Grain
    participant C as Consumer (StateCollection)

    P->>Q: Push(StateCollectionUpdate)
    Q->>Q: Сохранить в очередь
    Q->>C: Deliver через Observer
    C->>C: Обновить in-memory cache
```

### Использование

Основное применение — `StateCollection` sync:
- ID очереди: `StateCollectionDurableQueueId<TKey, TValue>`
- Payload: `StateCollectionUpdate<TKey, TValue>`
- Грейн вызывает `OnUpdatedTransactional()` -> push в DurableQueue -> все StateCollection-инстансы получают обновление

---

## RuntimePipe

**Назначение:** Request-response коммуникация между сервисами с таймаутом.

| Характеристика | Значение |
|---------------|----------|
| Тип грейна | `IGrainWithStringKey` |
| Таймаут | 5с (50с с дебаггером) |
| Observer | `IRuntimePipeObserver` |
| Keep-alive | Конфигурируемый |

### Как работает

```mermaid
sequenceDiagram
    participant C as Caller (Meta Gateway)
    participant P as RuntimePipe Grain
    participant H as Handler (Game Gateway)

    C->>P: Send<TResponse>(request)
    P->>H: Forward via Observer
    H->>H: Process request
    H->>P: Response
    P->>C: Return TResponse
    Note over C,P: Timeout: 5 секунд
```

### Использование

Основное применение — создание игровых сессий:
1. `MatchFactory` (Meta) отправляет pipe-запрос
2. `SessionEndpoints` (Game) обрабатывает запрос, создаёт сессию
3. Ответ с `SessionId` возвращается через pipe

---

## RuntimeChannel

**Назначение:** Широковещательный pub/sub для реалтайм-уведомлений.

| Характеристика | Значение |
|---------------|----------|
| Тип грейна | `IGrainWithStringKey` |
| Concurrency | `[AlwaysInterleave]` |
| Подписчики | ConcurrentDictionary |
| Observer | `IRuntimeChannelObserver` |

### Как работает

```mermaid
sequenceDiagram
    participant P as Publisher (Grain)
    participant Ch as RuntimeChannel Grain
    participant S1 as Subscriber 1
    participant S2 as Subscriber 2

    P->>Ch: Publish(data)
    par Broadcast
        Ch->>S1: Notify
        Ch->>S2: Notify
    end
```

### Использование

1. **Проекции пользователей:**
   - `UserProjection` публикует обновления через per-user channel
   - Клиент подписывается при подключении
   - ID канала: `UserProjectionChannelId`

2. **Распространение конфигов:**
   - Admin обновляет `CardConfigOptions`
   - Публикация через `AddressableStateChannelId`
   - Все сервисы получают обновлённый конфиг

---

## Сравнение паттернов

| Паттерн | Надёжность | Направление | Задержка | Применение |
|---------|-----------|-------------|----------|-----------|
| **DurableQueue** | Гарантированная | 1 -> N | Средняя | State sync |
| **RuntimePipe** | С таймаутом | 1 -> 1 | Низкая | Request-response |
| **RuntimeChannel** | Best-effort | 1 -> N | Низкая | Realtime уведомления |

## IMessaging

Фасад, агрегирующий все три паттерна:

```
IMessaging
  ├── DurableQueueClient  — Push / Listen
  ├── RuntimePipeClient   — Send / BindObserver
  └── RuntimeChannelClient — Publish / AddObserver
```

## Ключевые файлы

| Файл | Описание |
|------|----------|
| `backend/Infrastructure/Messaging/Messaging.cs` | Фасад IMessaging |
| `backend/Infrastructure/Messaging/Queues/DurableQueue.cs` | DurableQueue grain |
| `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs` | RuntimePipe grain |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs` | RuntimeChannel grain |
