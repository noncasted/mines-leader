# Shared протокол

Общий код между клиентом и бэкендом. Определяет модели данных, сетевые протоколы и конфигурации.

## Структура shared/

```
shared/
├── Domain/          — Enums (CardType, GameMatchType, SessionType, CharacterType)
├── Protocol/        — Базовые сетевые типы, union builder, MemoryPack
├── Backend/         — Протоколы Meta Gateway (auth, matchmaking, projections)
├── Session/         — Протоколы Game Gateway (session auth, players, objects)
├── Game/            — Игровые модели (board, cells, cards, player states, snapshots)
└── Configs/         — Конфигурации (cards, game modes, bots, rating)
```

---

## Domain Enums

### GameMatchType
```
Single = 10         — PvE (тренировка)
TimeLimited = 20    — PvP с индивидуальным таймером
LastManStanding = 30 — PvP с общим таймером
```

### CardType
10 карт (100-1010), каждая с обычной и Max версией. Подробнее: [[cards|Карточки]]

### SessionType
```
Lobby — Ожидание (динамическое число игроков)
Match — Матч (2 игрока)
```

### CharacterType
```
BOMJ, BIBA, BOBA, CHENOSOS
```

---

## Сериализация: MemoryPack

Весь сетевой обмен использует **MemoryPack** — бинарный формат с высокой производительностью.

### Union Types
Полиморфная сериализация через `[MemoryPackUnion(tag, typeof(T))]` на базовом интерфейсе. Теги явные и объявлены рядом с типами: каждый файл группы добавляет свои теги к `partial interface INetworkContext` (или `IEntityPayload`). Регистрации в рантайме нет, формат генерирует MemoryPack.

```csharp
[MemoryPackUnion(59, typeof(SharedSessionAuth.Request))]
[MemoryPackUnion(60, typeof(SharedSessionAuth.Response))]
public partial interface INetworkContext { }
```

Тег — это формат на проводе: не перенумеровывать и не переиспользовать, удалённый тип оставляет дыру. Дубль тега — ошибка генератора `MEMPACK012`, тип без тега ловит `Tests.Protocol.ProtocolSchemaTests`. Байты каждого типа и тега зафиксированы в `backend/Tools/Tests/Protocol/protocol_bytes.golden.txt` (бэкенд `ProtocolBytesTests` пишет, клиентский EditMode `ProtocolBytesTests` читает тот же файл).

### Версия протокола

`shared/Protocol/ProtocolVersion.cs`: `Hash` — первые 16 hex SHA-256 от схемы `backend/Tools/Tests/Protocol/protocol_schema.golden.txt` (union-теги, члены всех `[MemoryPackable]` типов shared, значения enum), `QueryKey = "protocol"`, `ClientOutdatedCloseCode = 4001`.
- `NetworkConnection.Run` дописывает `protocol=<Hash>` в URL каждого сокета (meta и game).
- Оба шлюза зовут `Common.Network.ClientProtocolCheck` до чтения чего-либо (`BackendConnectionMiddleware` до поиска юзера, `SessionConnectionMiddleware` до auth-запроса): другой или пустой хэш — сокет принимается и закрывается с 4001 (браузерный WebSocket не видит HTTP-статус отказа в апгрейде, только 1006).
- `IWebSocket.Closed` / `INetworkConnection.Closed` несут `SocketCloseInfo` (`Code`, `Reason`). На 4001 `NetworkConnection` зовёт `IClientOutdatedNotice.Show()` (`Global.UI.ClientOutdatedNotice`: блокирующий канвас "Update available", кнопка перезагружает страницу в вебе). `MetaLoop` после этого не переподключается.
- Изменили схему: `Schema_MatchesGolden` и `ProtocolHash_MatchesSchema` падают. Перезаписать golden (`UPDATE_PROTOCOL_GOLDEN=1`) и вписать новый `Hash` из сообщения теста. После деплоя каждая открытая старая вкладка увидит окно при следующем подключении.

---

## Сетевые протоколы

### Базовые сообщения (Protocol/)

```mermaid
graph TB
    subgraph "IMessageFromClient"
        OWC[OneWayMessageFromClient]
        RQC[RequestMessageFromClient<br>+ RequestId]
        RSC[ResponseMessageFromClient<br>+ RequestId]
    end

    subgraph "IMessageFromServer"
        OWS[OneWayMessageFromServer]
        RQS[RequestMessageFromServer<br>+ RequestId]
        RSS[ResponseMessageFromServer<br>+ RequestId]
    end
```

### Ошибки запросов (ErrorResponse)

`shared/Protocol/ErrorResponse.cs`: `ErrorResponse { ErrorCode Code; string Message }` (тег 75), `ErrorCode` = `Internal`, `UnknownCommand`, `InvalidRequest`, `Forbidden`, `NotFound`; `NetworkRequestException` (`Code`, `RequestName`).
- На каждый запрос есть ответ с обеих сторон. Game `CommandDispatcher`, meta `UserCommandsDispatcher` и клиентский `NetworkCommandsDispatcher` отвечают `ErrorResponse(UnknownCommand)`, если команды нет, и `ErrorResponse(Internal)`, если команда бросила исключение (стек в логе упавшей стороны). `SessionRequestException` (отказ сессии по правилам, например `CreateRequest` с чужим id) уходит со своим кодом и warning-ом. Упавшая one-way команда только логируется.
- `WriteRequest<T>` (клиентский и бэкенд `ConnectionWriter`) не возвращает null: `T` или `NetworkRequestException` (ErrorResponse), `TimeoutException`, `OperationCanceledException` (соединение закрыто), `InvalidOperationException` (ответ другого типа; на клиенте — запрос после `ForceSendAll`).
- Бэкенд `WriteOneWay` / `WriteResponse` — `void`: только постановка в очередь; закрытое соединение сообщение выбрасывает, переполнение отключает медленного клиента внутри writer-а.
- `EmptyResponse.Fail` остаётся для отказов по правилам матча (карта без цели, не твой ход): это ответ, а не ошибка запроса. Meta `ExecuteCommand` бросает и на нём.
- `IMatchmaking`: упавшая команда бросает из `Matchmaking.*`. `CreateGame`, `CreateGameWithBot`, `SearchLobby` ждут результат не дольше 10 с после ответа на команду. `SearchGame` ждёт без лимита; сбой создания матча из очереди клиенту не приходит (сервер возвращает игрока в очередь, вариант ошибки в `MatchResult` — N10).
- Сущности: клиент создаёт сущность только в своём диапазоне id (`backend/Game/Session/Objects/Entities/SessionEntityIds.cs`, `index * 100000 + counter`) и не с занятым или удалённым id, иначе `Forbidden`.

### Backend протоколы (Backend/)

| Контекст | Описание |
|----------|----------|
| `SharedBackendUserSignUp` | Регистрация (POST /develop_signup) |
| `SharedBackendUserLogin` | Логин (POST /login) |
| `SharedBackendSocketAuth` | WebSocket-аутентификация (UserId -> IsSuccess) |
| `SharedMatchmaking.*` | Поиск, создание матча, результат |
| `SharedBackendUser.*` | Проекции: Profile, Progression, Deck, Rating, Match |
| `SharedBackendProjection` | Обёртка для проекций |

### Session протоколы (Session/)

| Контекст | Описание |
|----------|----------|
| `SharedSessionAuth` | Аутентификация сессии (UserId + SessionId) |
| `SharedSessionPlayer.*` | LocalUpdate, RemoteUpdate, RemoteDisconnect |
| `SharedSessionObject.*` | SetProperty, PropertyUpdate, Event |

### Game протоколы (Game/)

| Контекст | Описание |
|----------|----------|
| `SharedGameAction.Open` | Открыть клетку |
| `SharedGameAction.OpenMultiple` | Chord click |
| `SharedGameAction.SetFlag` | Поставить флаг |
| `SharedGameAction.RemoveFlag` | Снять флаг |
| `SharedGameAction.CardUse` | Использовать карту |
| `SharedGameAction.SkipTurn` | Пропустить ход |

---

## Игровые модели

### Состояния игрока

| Модель | Поля | Описание |
|--------|------|----------|
| `PlayerHealthState` | Current, Max | Здоровье |
| `PlayerManaState` | Current, Max | Мана |
| `PlayerMovesState` | Left, Max, IsAvailable | Ходы |
| `PlayerDeckState` | Queue (List\<CardType\>) | Колода |
| `PlayerHandState` | Entries (List\<ActiveCard\>) | Рука |
| `PlayerStashState` | Count | Сброс |
| `PlayerModifiersState` | Dictionary\<Modifier, float\> | Модификаторы |

### Состояние раунда

| Модель | Режим | Поля |
|--------|-------|------|
| `GameRoundState` | Базовый | CurrentPlayer, SecondsLeft |
| `TimeLimitedRoundState` | TimeLimited | CurrentPlayer, SecondsLeft per player |
| `LastManStandingRoundState` | LastManStanding | CurrentPlayer, CurrentRound, SecondsLeft |

### Доска

| Модель | Поля | Описание |
|--------|------|----------|
| `BoardState` | Mines, Flags | Счётчики |
| `Position` | x, y | Координата (struct) |
| `BoardSnapshotRecord.*` | CellTaken, CellFree, Flag, MinesAround, Explosion, Effect* | Снапшоты изменений |

### Карты

| Модель | Описание |
|--------|----------|
| `ActiveCard` | Id (Guid) + Type (CardType) |
| `ICardUsePayload` | Базовый payload (union type) |
| `IBoardCardUsePayload` | + Position для карт по доске |
| `CardActionSnapshot.*` | Снапшоты результатов карт |
| `PatternShapes` | Генерация ромбовидных паттернов |
| `PlayerModifier` | Enum модификаторов (TrebuchetBoost) |

---

## Конфигурации

| Конфиг | Содержимое |
|--------|-----------|
| `CardConfigOptions` | Мана, цель, размер для каждой карты |
| `GameModeOptions` | HP, ходы, мана, время для каждого режима |
| `BotConfigOptions` | Задержки, лимиты действий бота |
| `RatingOptions` | Очки за победу/поражение |
| `ProgressionOptions` | XP за победу/поражение |
| `BoardOptions` | Размер поля (16), мин (40) |

---

## Ключевые файлы

| Файл | Описание |
|------|----------|
| `shared/Domain/*.cs` | Все domain enums |
| `shared/Protocol/Messages.cs` | Базовые сетевые типы |
| `shared/Protocol/INetworkContext.cs` | Базовый union протокола (теги в файлах групп) |
| `shared/Backend/SharedMatchmaking.cs` | Протокол матчмейкинга |
| `shared/Session/SharedSessionObject.cs` | Синхронизация объектов |
| `shared/Game/SharedGameAction.cs` | Все игровые действия |
| `shared/Game/Player/*.cs` | Состояния игрока |
| `shared/Game/Cards/*.cs` | Модели карт |
| `shared/Configs/*.cs` | Все конфигурации |
