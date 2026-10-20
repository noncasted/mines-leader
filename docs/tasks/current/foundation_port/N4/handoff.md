# N4. ErrorResponse вместо зависания запросов: handoff

Статус: review. Бэкенд проверен (сборка, полный прогон). Клиент проверен только чтением: компиляцию, EditMode и живую проверку делает оркестратор.
Источник: peace-data-reborn `1a7d6c0b1` (F2.3, F2.16, F2.17, F2.24, F2.38). Проверка `CreateRequest` взята из `742f8168f` (F2.19, только `EntityCreateCommand` и `SessionRequestException`), по решению оркестратора.
Worktree после `git merge --ff-only main` (HEAD 65801f3e, N6 и N7 уже там).

## Шаг 0: баги

Все подтверждены чтением кода.

- Game: `CommandDispatcher` берёт команду индексатором и не ловит исключения. `ExecutionQueue` логирует исключение и проглатывает его. Если запрос упал или для него нет команды, ответа нет, и клиент ждёт 20 с, а потом получает `null`.
- Meta: `UserCommandsDispatcher` на исключение отвечает `EmptyResponse.Fail`. Клиентский `WriteRequest<T>` с типизированным ответом (`MatchHistoryResponse` и др.) видит не тот тип: пишет "got no response" и возвращает `null`. Неизвестная команда даёт `KeyNotFound` в catch, ответ тот же.
- F2.38: `UserConnectionEntryPoint` подписывается на `View` конфигов и выбрасывает результат `ValueTask` от `WriteOneWay`. В `Write` стоит `.GetAwaiter().GetResult()`, и на закрытом соединении там летит исключение.
- Клиентский `ConnectionWriter.WriteRequest`:
  - на каждый запрос добавляет `_lifetime.Listen`, который не снимается, поэтому подписки копятся всё время жизни соединения;
  - `AwaitTimeout` не отменяется;
  - возвращает `default` вместо исключения.
- Клиентский `NetworkCommandsDispatcher`: если серверный запрос упал, ответ не уходит. `ConnectionReader` молча отбрасывает неизвестный тип сообщения.
- `BackendEndpoints.ExecuteCommand`: если ответа нет или пришёл `HasError`, он только пишет `LogError` и возвращает управление. После этого `Matchmaking.CreateGameWithBot`/`CreateGame`/`SearchLobby`/`SearchGame` ждут `WaitOnce` вечно. Пример: `GameMock` при сбое `CreateWithBot` (B1: `BotFactory.AssignDeck`, `match.Setup` бросают) висит на старте.
- `EntityCreateCommand` принимает любой id. Клиент пользователя 1 может прислать `CreateRequest` с id `2*100000+1`, это id игрока пользователя 2. Тогда `GameFlow` → `PlayerFactory` → `EntityBuilder.Build` падает на занятом id, и матч не стартует. Честный клиент B `CreateRequest` не шлёт: у `AddLocalEntity` и `INetworkEntityFactory.Send` нет вызовов (перепроверено grep-ом). Значит, запрет игру не ломает.

## Что сделано

Shared:
- `shared/Protocol/ErrorResponse.cs` (+ `.meta`): `ErrorCode`, `ErrorResponse`, `NetworkRequestException`, как в A. Union-тег **75**: тег A `1` в B занят, 0..74 заняты.
- `EmptyResponse` **не менялся** (A убрал `HasError`/`Fail`). В B `EmptyResponse.Fail` — ответ на отказ по правилам матча: около 60 мест в картах и командах, `CardLocalDrag`, `GameAgentBridge`, тесты карт. Это не ошибка запроса.

Бэкенд:
- `Common/Network/ConnectionWriter.cs` как в A:
  - `WriteOneWay` и `WriteResponse` стали `void`;
  - `WriteRequest<T>` возвращает `Task<T>` и бросает `NetworkRequestException`, `InvalidOperationException`, `TimeoutException`, `OperationCanceledException`.
- `Game/Session/Users/CommandDispatcher.cs`: на каждый запрос отвечает. Если команды нет — `UnknownCommand`, если бросила — `Internal`, если `SessionRequestException` — его код и warning. Упавшая one-way команда логируется с контекстом. `UserFactory` передаёт логгер.
- `Game/Session/Commands/SessionRequestException.cs` (новый, как в A).
- `Orchestration/MetaGateway/UserFlow/Commands/UserCommandsDispatcher.cs`: то же для meta, вместо `EmptyResponse.Fail`.
- `UserConnectionEntryPoint`: убран `.GetAwaiter().GetResult()`.
- `BotConnection`: новые сигнатуры. `WriteRequest` бросает `InvalidOperationException` (у бота нет клиента; вызовов нет).
- `EntityCreateCommand`: id вне диапазона создателя или уже занятый/удалённый id → `SessionRequestException(Forbidden)`.
  - Диапазон берётся из нового `Game/Session/Objects/Entities/SessionEntityIds.cs` (`Stride = 100_000`, как и раньше в `EntityBuilder`; `EntityBuilder` теперь берёт константу оттуда).

Клиент:
- `ConnectionWriter.WriteRequest` как в A:
  - на закрытом соединении бросает `OperationCanceledException`, после `ForceSendAll` — `InvalidOperationException`;
  - подписка на lifetime снимается по завершении запроса, таймаут 20 с отменяется ответом;
  - `ErrorResponse` превращается в `NetworkRequestException`, ответ чужого типа — в `InvalidOperationException`;
  - `Close()` отменяет ожидающие запросы, а не завершает их `null`.
- `ConnectionReader`: на null или неизвестный тип сообщения бросает исключение.
- `NetworkCommandsCollection.TryGet`. `NetworkCommandsDispatcher` отвечает на серверный запрос `ErrorResponse` (сервер запросов клиенту сейчас не шлёт, это симметрия).
- `SessionConnection`: проверка `response == null` убрана, ответ больше не бывает null.
- `BackendEndpoints`:
  - `ExecuteCommand` бросает исключение на ошибку и на `HasError` (`InvalidOperationException`), чтобы вызывающий не ждал результат, которого не будет;
  - в `ClaimAchievementReward` убрана проверка на null, отказ по `HasError` остался.
- `Matchmaking`:
  - упавшая команда бросает исключение;
  - `CreateGame`, `CreateGameWithBot` и `SearchLobby` ждут результат не дольше 10 с **после ответа** на команду: сервер создаёт сессию внутри команды и шлёт результат до ответа;
  - `SearchGame` ждёт без лимита, потому что поиск в очереди может идти долго.
- `MenuPlay.Search`:
  - `OperationCanceledException` глотается только при остановленном поиске. Раньше он глотался всегда, и при обрыве соединения экран оставался в "searching";
  - lifetime поиска взят в локальную переменную, чтобы поздний ответ остановленного поиска не сбросил следующий.
  - Ошибка с сервера: экран поиска возвращается в idle, исключение уходит в консоль (как было для падения meta).
- `AchievementRewards.GetOptions` сохраняет свой контракт "null при сбое запроса" (окно наград закрывается): ловит `NetworkRequestException`, `TimeoutException`, `OperationCanceledException` и пишет `LogException`.
- `.Forget()` у fire-and-forget запросов в `BoardActions`, `LastManStandingRound`, `TimeLimitedGameRound`, `GameCheatsWindow`. Раньше сбой был `LogError` по таймауту в writer. Теперь это исключение задачи, а без `Forget` оно всплыло бы только из финализатора.

Тесты:
- Новый `Tools/Tests/Network/CommandErrorResponseTests.cs` — порт A на реальном websocket, 7 тестов. Добавлен `Game_CommandRefuses_ClientGetsItsCode` из `742f8168f`. Типы `SharedSessionLatency` заменены на `SharedSessionPlayer.LocalUpdate/RemoteDisconnect`, `SharedMatchmaking.CreateLobby` — на `SearchLobby`.
- `Tools/Tests/Network/ConnectionWriterTests.cs` — патч A (`..._WritesDropped`, `ErrorResponse_ThrowsNetworkRequestException`, `ResponseOfAnotherType_ThrowsInvalidOperation`), типы те же, что были в B.
- Новый `Tools/Tests/Game/SessionEntityIdsTests.cs`, 8 тестов. Среди них `Create_PlayerIdOfAnotherUser_IsForbidden_SessionStillBuildsThePlayer`: чужой id игрока отклонён, после этого сессия строит игрока с этим id.
- `UserConnectionEntryPointTests`: мок `WriteOneWay` через `When/Do`.
- Клиент EditMode:
  - `ConnectionWriterTests`: вместо `SocketClosed_PendingAndNextRequestsGetNullAtOnce` теперь `SocketClosed_PendingAndNextRequestsAreCancelledAtOnce`; новые `LifetimeEnds_PendingRequestIsCancelled`, `Request_ErrorResponse_ThrowsNetworkRequestException`, `Request_OtherResponseType_ThrowsInvalidOperation`, `ForceSendAll_ThenRequestThrowsInvalidOperation`;
  - `NetworkConnectionTests.SocketLostInsideConnect_ConnectionIsClosed` теперь ждёт `OperationCanceledException` вместо null и LogError.
- Документация: `docs/obsidian/architecture/shared-protocol.md`, раздел "Ошибки запросов (ErrorResponse)".

## Протокол: хэш и golden

- `ProtocolVersion.Hash`: `ddae8a7e3afb4a39` → `522f7664226053dd`.
- `protocol_schema.golden.txt` перезаписан (`UPDATE_PROTOCOL_GOLDEN=1`): +`enum Shared.ErrorCode`, +`type Shared.ErrorResponse`, +`union Shared.INetworkContext 75 Shared.ErrorResponse`.
- `protocol_bytes.golden.txt` перезаписан так же. Добавлены только две строки: `type Shared.ErrorResponse` и `union ... 75 Shared.ErrorResponse`. Существующие строки побайтно те же: `EmptyResponse` и все теги не менялись. Формат старых сообщений прежний, клиентский `ProtocolBytesTests` читает тот же файл.

## Проверка

- `dotnet build backend/backend.slnx`: 0 ошибок.
- Новые и затронутые тесты (`CommandErrorResponseTests`, `SessionEntityIdsTests`, `ConnectionWriterTests`, `UserConnectionEntryPointTests`, `SessionObjectsTests`): 43/43.
- Протокол (`ProtocolSchemaTests`, `ProtocolBytesTests`, `SharedSerializationTests`, `ClientProtocolCheckTests`): 578/578.
- Полный прогон: 1831/1831, пропущенных 0 (Docker был доступен).
- Клиент не собирался: в worktree нет `.csproj`.

## Что проверить в редакторе

1. Компиляция. `Shared.ErrorCode` в клиенте пишется с префиксом там, где рядом `using UnityEngine`. Pattern `when (a || b)` в `AchievementRewards`.
2. EditMode: `Internal.Tests.ConnectionWriterTests` (11 тестов), `NetworkConnectionTests` (3), `ProtocolBytesTests` (с новыми строками golden).
3. Живая проверка без ошибок: вход, матч с ботом через меню (поиск → бот по таймеру), ходы, выход. Хэш протокола новый, бэкенд пересобрать.
4. Ошибка сервера в matchmaking UI (временная правка, потом откатить):
   - бэкенд: в `backend/Orchestration/MetaGateway/Matchmaking/MatchmakingCommands.cs`, `SearchMatch.Execute`, первой строкой `throw new InvalidOperationException("N4 probe");`;
   - меню, Play → Search: экран сразу возвращается в idle (не "searching"). В консоли клиента исключение `NetworkRequestException: Request SharedMatchmaking.SearchMatch failed on the other side: Internal: SharedMatchmaking.SearchMatch: InvalidOperationException: N4 probe`. В логе meta-шлюза `[User] [Command] Request command ... failed` со стеком. Повторный Search даёт то же, без зависаний.
   - То же для `GameMock`: бросить в `CreateWithBot.Execute`. `GameMock.Process` падает с `NetworkRequestException` сразу, а не висит.
5. Game-сессия: бросить в любой `ResponseCommand` сессии нельзя без правки игры. Достаточно, что `CardUseCommand` и другие ходы работают как раньше: доменные ошибки ходов по-прежнему приходят `EmptyResponse.Fail`.

## Отличия от A

- Тег `ErrorResponse` 75, а не 1.
- `EmptyResponse.HasError/Fail` оставлены, в B это игровой отказ. Поэтому `ExecuteCommand` дополнительно бросает на `HasError`.
- Проверка диапазона id своя: в B `SharedEntityIds` нет, диапазоны клиента (×10000, мёртвый код) и сервера (×100000) не согласованы. Проверка опирается на серверный диапазон `SessionEntityIds` (×100000), его же использует `EntityBuilder`. `SharedEntityIds` из A не переносился (N11).
- Не перенесено из `1a7d6c0b1`:
  - `UserFactory`/`ResolvedUser.Projections` (handshake больше не читает проекции, F2.14 follow-up). Это не ErrorResponse. В B handshake всё ещё читает проекции и выбрасывает их (`OnConnected` их игнорирует после N3): одна лишняя поездка в БД на вход. Можно отдельной мелкой задачей.
  - `.claude/docs/NETWORK.md`: в B такого файла нет, раздел добавлен в `shared-protocol.md`.
  - `MenuLobbyEntry`, `LobbyJoinMatchView`: в B их нет.
- `Matchmaking`: таймаут результата считается после ответа на команду, а не от отправки (как в A). Так медленное создание матча на холодном кластере не превращается в таймаут: ответ на команду ограничен своими 20 с. `SearchGame` без лимита (в A поиска в очереди нет).
- Добавлено для B (в A нет): `.Forget()` у fire-and-forget запросов игры, правка `MenuPlay` (OCE и локальный lifetime поиска), обёртка в `AchievementRewards.GetOptions`.

## Для оркестратора

- B1 (явный провал матча): ErrorResponse покрывает только команды, где матч создаётся внутри запроса (`CreateWithBot`, `Create`, `SearchLobby`). Ошибка уходит клиенту исключением, matchmaking UI возвращается в idle. Матч из очереди (`SearchMatch` → `Matchmaking.Loop` → `CreateOrRequeue`) создаётся уже после ответа на команду. Естественного пути сообщить об этом через ErrorResponse нет: нужен вариант ошибки в `MatchResult` или отдельное сообщение. Это N10, там `MatchResult` и так меняется под корреляцию. Сейчас игрок остаётся в поиске, сервер возвращает его в очередь и логирует (B1).
- Остаётся: клиент может занять id **своего** диапазона, который сессия потом выдаст его же игроку (`index*100000+1`). Тогда `Build` падает, и ломается только свой матч. Закроется, когда сущности клиента получат свой диапазон (`SharedEntityIds`, N11) или сервер будет выдавать id из своего счётчика.
- `Profile.LoadHistory/LoadDetails` теперь при сбое meta бросают исключение, а не возвращают null. `MenuProfile` (UniTaskVoid) пишет его в консоль, история остаётся пустой, видимое поведение то же. Старые проверки `response?.Matches == null` теперь недостижимы, не трогал.
- Протокольный релиз: N4 меняет хэш. Деплой вместе с N6/N7/S3/S4/N10.
