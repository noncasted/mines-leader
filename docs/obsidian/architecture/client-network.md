# Клиент: сетевой слой

## Обзор

```mermaid
graph TB
    subgraph Client
        UI[UI Layer]
        MS[Meta Services]
        GS[Game Services]

        subgraph Network["Network Layer"]
            BC[BackendClient<br>HTTP REST]
            MB[MetaBackend<br>WebSocket]
            NS[NetworkSession<br>WebSocket]
        end
    end

    subgraph Backend
        MetaGW[Meta Gateway]
        GameGW[Game Gateway]
    end

    BC <-->|HTTP| MetaGW
    MB <-->|WebSocket| MetaGW
    NS <-->|WebSocket| GameGW

    MS --> BC
    MS --> MB
    GS --> NS
```

---

## Два канала связи

### 1. Meta Backend (постоянное WebSocket-соединение)
**Файл:** `client/Assets/Meta/Connection/MetaBackend.cs`

Постоянное соединение с Meta Gateway для:
- Авторизация (`SharedBackendSocketAuth`)
- Матчмейкинг (`SharedMatchmaking`)
- Проекции пользователя (профиль, рейтинг, прогрессия)

### 2. Game Session (WebSocket на время матча)
**Файл:** `client/Assets/Common/Network/Session/Root/NetworkSession.cs`

Временное соединение с Game Gateway на время матча:
- Аутентификация сессии (`SharedSessionAuth`)
- Игровые команды (`SharedGameAction`)
- Синхронизация состояний (`SharedSessionObject`)

---

## WebSocket-реализации

**Файл:** `client/Assets/Common/Network/Connections/NetworkConnection.cs`

| Платформа | Реализация |
|-----------|-----------|
| Editor, iOS, Android | `DefaultWebSocket` (.NET WebSocket) |
| Web, ItchIO | `JsWebSocket` (JavaScript interop) |

`NetworkConnection` абстрагирует:
- Reader — чтение входящих сообщений
- Writer — отправка исходящих
- Dispatcher — маршрутизация по типу сообщения

---

## Протокол сообщений

Все сообщения сериализуются через **MemoryPack** (бинарный формат).

### Типы сообщений

```mermaid
graph TB
    subgraph FromClient["Client -> Server"]
        OWC[OneWayMessage<br>fire-and-forget]
        RQC[RequestMessage<br>ожидает ответ]
        RSC[ResponseMessage<br>ответ на запрос сервера]
    end

    subgraph FromServer["Server -> Client"]
        OWS[OneWayMessage<br>push-уведомления]
        RQS[RequestMessage<br>запрос к клиенту]
        RSS[ResponseMessage<br>ответ на запрос клиента]
    end
```

Каждый request имеет `RequestId` для корреляции запрос-ответ.

---

## Авторизация

### HTTP-авторизация
```mermaid
sequenceDiagram
    participant C as Client
    participant M as Meta Gateway

    C->>M: POST /develop_signup (или /login)
    M->>C: Response (UserId)
```

### WebSocket-авторизация (Meta)
```mermaid
sequenceDiagram
    participant C as Client
    participant M as Meta Gateway

    C->>M: WebSocket upgrade (?userId=...)
    Note over M: IUserFactory.Resolve: юзер и его проекции одной транзакцией
    M->>C: SharedBackendProjection[] (проекции юзера + InitialCardPreviews)
    M->>C: SharedBackendProjection[] (конфиги)
```

### WebSocket-авторизация (Game Session)
```mermaid
sequenceDiagram
    participant C as Client
    participant G as Game Gateway

    C->>G: WebSocket connect
    C->>G: SharedSessionAuth.Request (UserId, SessionId)
    G->>C: SharedSessionAuth.Response (IsSuccess)
    G->>C: SharedSessionPlayer.LocalUpdate (Index)
    G->>C: SharedSessionPlayer.RemoteUpdate (other players)
    G->>C: SharedSessionObject.SetProperty[] (initial state)
```

---

## Проекции (BackendProjection)

**Файл:** `client/Assets/Meta/Connection/BackendProjectionHub.cs`

Сервер отправляет проекции пользователя через Meta WebSocket:

| Проекция | Данные |
|----------|--------|
| `ProfileProjection` | Id, Name |
| `ProgressionProjection` | Experience |
| `DeckProjection` | Entries{}, SelectedIndex |
| `RatingProjection` | Rating value |
| `Match` | History records |

Проекции приходят как `SharedBackendProjection`, развёрнутые в конкретные типы `INetworkContext`.

---

## Синхронизация игрового состояния

Во время матча все состояния синхронизируются через `SharedSessionObject`:

| Сообщение | Описание |
|-----------|----------|
| `SetProperty` | Начальное значение (ObjectId + PropertyId + Value) |
| `PropertyUpdate` | Обновление с версионированием |
| `Event` | Событие объекта |

Каждый игровой объект (Player, Board, Hand) — `Entity` с набором `ValueProperty<T>`. Изменение свойства на сервере автоматически отправляет `PropertyUpdate` клиенту.

## Ключевые файлы

| Файл | Описание |
|------|----------|
| `client/Assets/Common/Network/Connections/NetworkConnection.cs` | Базовое соединение |
| `client/Assets/Common/Network/Sockets/DefaultWebSocket.cs` | .NET WebSocket |
| `client/Assets/Common/Network/Sockets/JsWebSocket.cs` | JS WebSocket |
| `client/Assets/Meta/Connection/MetaBackend.cs` | Meta-соединение |
| `client/Assets/Meta/Connection/BackendProjectionHub.cs` | Hub проекций |
| `client/Assets/Common/Network/Session/Root/NetworkSession.cs` | Игровая сессия |
| `client/Assets/Global/Backend/Client/BackendClient.cs` | HTTP REST клиент |
