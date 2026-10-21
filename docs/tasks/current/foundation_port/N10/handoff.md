# N10. Корреляция результата матча и лобби, провал создания матча из очереди: handoff

Статус: review. Бэкенд проверен: сборка и полный прогон. Клиент проверен только чтением. Компиляцию, EditMode и живые сценарии проверяет оркестратор.
Источник: peace-data-reborn `f6f6fd317` (F2.15), хвост B1 (`B1/handoff.md`, раздел "Matchmaking: провал создания матча").
Worktree после `git merge --ff-only main`, HEAD 2138cb8e (S3+S4 уже там).

## Шаг 0: баги

Все подтверждены чтением кода.

1. **Чужой результат поиска.** `client/Assets/Meta/Matchmaking/Matchmaking.cs`: `SearchGame` ждёт `_gameResultProjection.WaitOnce`, то есть первый пришедший `MatchResult`. Id запроса в нём нет. Сценарий:
   - игрок отменил поиск, когда цикл очереди уже вынул его и создавал матч;
   - игрок сразу начал новый поиск;
   - результат прошлого поиска принимается за ответ на новый. Игрок входит в матч, который ему не предназначался (или чей тикет уже истёк).
   
   То же для `CreateGame`, `CreateGameWithBot`, `SearchLobby`. Они ждали ту же одноразовую проекцию с таймаутом 10 с. Поздний результат прошлого вызова принимался за текущий, а сервер их не различал.
2. **Провал создания матча из очереди не доходит до клиента** (хвост B1). `Matchmaking.CreateOrRequeue` логировал ошибку и возвращал игроков в очередь через `SearchMatch`. Клиент оставался в поиске и ничего не знал. При постоянном сбое (нет game-сервера, сломан грейн) цикл повторял создание каждые ~100 мс (PvP) или каждые `MatchmakingApplyThreshold` (бот), и каждая попытка писала `LogError`.
3. Мелочь: если отправка результата второму участнику падала, первый, который его уже получил, всё равно возвращался в очередь.

## Что сделано

### Протокол (shared)

- `SharedMatchmaking.SearchMatch.RequestId` (Guid). Клиент выбирает его на каждый поиск.
- `SharedMatchmaking.MatchResult`:
  - новое поле `RequestId`: id поиска; в ответе на команду `Guid.Empty`;
  - новое поле `Status` (`MatchSearchStatus`).
- Новый enum `SharedMatchmaking.MatchSearchStatus { Matched = 0, Failed = 1 }`. При `Failed` заданы только `Type`, `RequestId` и `Status`.
- Новых `INetworkContext` нет, поэтому новых union-тегов нет. Теги 18-24 те же.
- `SearchLobby`, `Create`, `CreateWithBot` теперь отвечают результатом: `LobbyResult` / `MatchResult` вместо `EmptyResponse`. Корреляцию даёт id запроса в writer-е. Проекция нужна только `SearchMatch`.
- `ProtocolVersion.Hash`: `160117fe01eb67ed` → `701d30859db55520`. Golden-файлы `protocol_schema.golden.txt` и `protocol_bytes.golden.txt` перезаписаны (`UPDATE_PROTOCOL_GOLDEN=1`). Изменились только `SearchMatch`, `MatchResult` (type и union) и новый enum.

### Бэкенд

- `Meta/Matches/MatchFactory.cs`:
  - `Create` и `CreateWithBot` возвращают `CreatedMatch` (`SessionId`, `ServerUrl`, `Type`, `Tickets` на каждого человека, `ToResult(userId, requestId)`);
  - сами больше ничего не шлют. Доставляет результат вызывающий.
- `Meta/Matches/LobbyFactory.cs`: `GetOrCreate` возвращает `SharedMatchmaking.LobbyResult`.
- `Meta/Matches/LobbySearchResultUpdate.cs` удалён (`git rm`).
- `Meta/Matches/MatchSearchResultUpdate.cs`: одноразовая проекция конца поиска из очереди. Поля: `RequestId`, `Status`, `Type`, `SessionId`, `ServerUrl`, `Ticket`. Фабрики `Matched(match, userId, requestId)` и `Failed(type, requestId)`.
- `Orchestration/MetaGateway/Matchmaking/Matchmaking.cs`:
  - `SearchQueueEntry.RequestId`. Повторный `SearchMatch` заменяет запись, и в результат уходит id последнего поиска;
  - `SearchMatch(userId, type, requestId)`. `Create`, `CreateWithBot`, `SearchLobby` возвращают результат;
  - `CreateOrRequeue` → `CreateAndNotify`. Создание падает → `LogError` с исключением, каждому участнику уходит `Failed` со своим `RequestId`. **Возврата в очередь нет.** Создание прошло → каждому `Matched` с его тикетом и `RequestId`. Сбой доставки одному участнику логируется (`LogError`) и не мешает другому;
  - в конструктор добавлен `IMessaging`.
- `MatchmakingCommands`:
  - `SearchLobby`, `Create`, `CreateWithBot` отвечают результатом;
  - `SearchMatch` без `RequestId` (`Guid.Empty`) бросает `ArgumentException`, клиент получает `ErrorResponse` (N4).

### Клиент

- `Meta/Connection/BackendEndpoints.cs`:
  - `SearchGame(type, requestId)`;
  - `CreateGame`, `CreateGameWithBot` и `SearchLobby` идут через `Connection.Writer.WriteRequest<MatchResult / LobbyResult>`. Ошибка сервера даёт `NetworkRequestException`, нет ответа за 20 с — `TimeoutException` (N4/N5).
- `Meta/Matchmaking/Matchmaking.cs`:
  - `SearchGame`: новый `requestId`, подписка на проекцию до команды. Результат с чужим `RequestId` пропускается с `Debug.LogWarning("[Matchmaking] Ignored the <Status> result of search <id>: waiting for search <id>")`. `Status = Failed` бросает `InvalidOperationException`;
  - `Request<T>` с `ResultTimeout` 10 с и проекция `LobbyResult` удалены. Остальные методы: `EnsureConnected`, затем запрос.
- `Meta/MetaScopeExtensions.cs`: `RegisterBackendResponse<LobbyResult>` убран. `MatchResult` остаётся (проекция поиска).
- `MenuPlay` не менялся. Его `catch (Exception) when (searchLifetime.IsTerminated == false)` уже возвращает экран поиска в idle и пробрасывает исключение, и ошибка видна в консоли.

### Документация

`docs/obsidian/game/match-flow.md` (сообщения, провал из очереди), `docs/obsidian/architecture/shared-protocol.md` (контракт `IMatchmaking`).

## Решение: провал клиенту, а не возврат в очередь

- Возврат в очередь при постоянном сбое крутит цикл, пишет лог на каждой попытке, а игрок ждёт без конца. Это противоречит fail fast.
- При разовом сбое игрок увидит провал и нажмёт поиск снова. Это одно действие, и ошибка видна.
- PvP-пара: провал получают оба. Второй участник ни в чём не виноват, но вернуть его в очередь одного значило бы оставить часть старого поведения. Он тоже нажмёт поиск снова.
- Сбой после создания (доставка одному участнику) — это не провал матча. Сессия создана, у второго есть тикет. Первый, скорее всего, уже отключён. Если нет, он остаётся в поиске без результата, как и до N10. Сессия закроется по `JoinTimeout`.

## Отличия от A

- В A фонового цикла матчмейкинга нет, всё — ответ на команду (`SessionRequester`). В B очередь живая, поэтому поиск остаётся проекцией и коррелируется `RequestId`. Синхронные команды (`SearchLobby`, `Create`, `CreateWithBot`) стали ответами на команду, как `LobbyResult` в A.
- `SessionTicketRequest` / `SessionTicketResponse` не перенесены. В B тикет всегда приходит вместе с адресом сессии: `MatchResult`, `LobbyResult` или `RematchContexts.Success` от game-сервера. Сценария A "лобби стартовало матч, тикет запрашивается отдельно" в B нет.
- `LobbyStatus` (NotFound/Full/Started) из A не нужен, в B нет кодов лобби. Провал матча из очереди — `MatchSearchStatus.Failed`, в A аналога нет.
- Клиентский таймаут результата 10 с (`ResultTimeout`, как "lobby result timeout 10 s" в A) не нужен: команды ждут ответа с общим таймаутом writer-а 20 с.

## Проверка

- `dotnet build backend/backend.slnx`: 0 ошибок.
- `MatchmakingTests`: 13/13. Новые и переписанные тесты:
  - `Create_ReturnsResultOfTheUser`, `CreateWithBot_ReturnsResultOfTheUser`: ответ команды, проекций нет;
  - `SearchLobby_ReturnsLobbyFactoryResult`;
  - `Loop_PairMatched_EachGetsResultWithOwnRequestId`;
  - `Loop_SearchRepeated_ResultCarriesLatestRequestId`;
  - `Loop_MatchCreationFails_BothGetFailedAndNoRetry`: заменяет `Loop_MatchCreationFails_LogsAndReturnsPlayersToQueue`. На старом коде падает: старый код создавал матч повторно;
  - `Loop_BotMatchCreationFails_PlayerGetsFailed`;
  - `Loop_DeliveryToOneFails_OtherStillGetsResult`;
  - `SearchMatchCommand_WithoutRequestId_Throws`.
- Протокол: 589/589 после обновления golden и хэша.
- Полный прогон: **1890/1890**, пропущенных 0. Docker был доступен.
- Клиент не собирался: в worktree нет `.csproj`. Чтением проверено:
  - `Advise(lifetime, OnResult)` с локальной функцией `void OnResult(MatchResult)` — тот же перегруз, что в `WaitOnce` (`EventSourceExtensions.Advise<T1, T2>(..., Action<T2>)`);
  - `Debug` берётся из `UnityEngine`, других `Debug` в namespace нет;
  - других реализаций `IMatchmaking` и потребителей `IBackendProjection<LobbyResult>` нет;
  - `GameMock.CreateGameWithBot` и `MenuPlay.SearchGame` — сигнатуры те же.
- Клиентских EditMode-тестов не добавлял. Сборка `Meta` не подключена ни к одной тестовой asmdef (есть только `Internal.Tests`). Тест `Matchmaking` потребовал бы новую asmdef и фейки `IMetaBackend`/`INetworkConnection`/`IConnectionWriter`, а скомпилировать их без редактора нельзя. Протокол на клиенте покрывает `ProtocolBytesTests` (читает тот же golden).

## Что проверить в редакторе

1. Компиляция без ошибок. Генератор контейнера должен принять `Matchmaking` без параметра `IBackendProjection<LobbyResult>`.
2. EditMode: `Internal.Tests.ProtocolBytesTests` (golden новый), `ConnectionWriterTests`, `NetworkConnectionTests`.
3. Живые сценарии на свежем Aspire из этой ветки. Бэкенд пересобрать: хэш новый.
   1. **GameMock** (`CreateWithBot` ответом на команду). Матч стартует, в консоли нет `No MatchResult in 10 s`. Game: `[Game] User connected`.
   2. **Меню → Search → бот по таймеру** (20 с). Матч стартует. Meta: `[Matchmaking] <id> is searching for a game, request <requestId>`, затем `applying bot match`.
   3. **Отмена и повторный поиск.** Search → Cancel → Search.
      - Новый поиск заканчивается своим матчем.
      - Чужой результат воспроизводится только если отмена попала в окно создания матча (меньше секунды). Временный способ: в `MatchFactory.CreateWithBot` перед `return` добавить `await Task.Delay(3000)`, `MatchmakingApplyThreshold` = 1. Search, через ~1.5 с Cancel, сразу Search.
      - Ожидается warning `[Matchmaking] Ignored the Matched result of search <old>: waiting for search <new>`, затем второй матч по новому поиску. Потом откатить задержку.
   4. **Провал создания из очереди.** Временно в `MatchFactory.CreateWithBot` первой строкой `throw new InvalidOperationException("N10 test")`. Search, ждать бот-матча.
      - Meta: один `LogError` `[Matchmaking] Failed to create a ... match for [...], the search fails` с исключением. Повторов раз в 100 мс / 20 с нет.
      - Клиент: экран поиска вернулся в idle, в консоли `InvalidOperationException: [Matchmaking] The server failed to create the ... match of search <id>: Failed`.
      - Повторный Search снова даёт один провал. Откатить.
   5. **Два клиента (MPPM), PvP.** Оба Search в TimeLimited, матч, у каждого свой тикет. В meta-логе у двух поисков разные `request`.
   6. **Реванш** (с ботом и PvP): не менялся, `MatchResult` собирается на клиенте из `RematchContexts.Success`, `Status` по умолчанию `Matched`.

## Деплой

- Протокольный релиз вместе с N4/N6/N7/S3/S4: бэкенд, сразу за ним WebGL. Новый хэш `701d30859db55520`. Старые клиенты получают 4001 и окно обновления.
- Новых секретов, переменных и миграций нет. `MatchSearchResultUpdate` и `MatchFactory` — транзиентные сообщения (messaging), в БД не пишутся.
- Мониторинг meta: `[Matchmaking] Failed to create a` (провал отдан клиенту, без повторов), `Failed to send the`.

## Изменённые файлы

- `shared/Backend/SharedMatchmaking.cs`, `shared/Protocol/ProtocolVersion.cs`
- `backend/Meta/Matches/MatchFactory.cs`, `LobbyFactory.cs`, `MatchSearchResultUpdate.cs`, `LobbySearchResultUpdate.cs` (удалён)
- `backend/Orchestration/MetaGateway/Matchmaking/Matchmaking.cs`, `MatchmakingCommands.cs`
- `backend/Tools/Tests/Meta/MatchmakingTests.cs`, `backend/Tools/Tests/Protocol/protocol_schema.golden.txt`, `protocol_bytes.golden.txt`
- `client/Assets/Meta/Matchmaking/Matchmaking.cs`, `client/Assets/Meta/Connection/BackendEndpoints.cs`, `client/Assets/Meta/MetaScopeExtensions.cs`
- `docs/obsidian/game/match-flow.md`, `docs/obsidian/architecture/shared-protocol.md`
- `docs/tasks/current/foundation_port/N10/handoff.md` (новый)
