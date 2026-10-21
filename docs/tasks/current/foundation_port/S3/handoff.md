# S3 + S4. Гостевой секрет и подписанные тикеты сессий: handoff

Статус: review. Бэкенд проверен: сборка и полный прогон. Клиент проверен только чтением. Компиляцию, EditMode и живые сценарии проверяет оркестратор.
Источник: peace-data-reborn `f6f6fd317` (F1.1, F1.2), раздел A `.claude/docs/DEPLOY.md` "Guest secret migration (F1.2)".
Worktree после `git merge --ff-only main`, HEAD 6d8aecf9 (N4, N6, N7 уже там).

## Шаг 0: баги

Все подтверждены чтением кода.

- S3, вход по голому id. `BackendConnectionMiddleware.TryReadUserId` брал `?userId=` из query, а `UserFactory.Resolve` входил в существующего юзера без проверки. Достаточно знать чужой Guid.
- S3, утечка Guid. Она шире, чем в задаче:
  - `Session.cs` рассылал `RemoteUpdate.BackendId = other.Id`;
  - в game-сессии `IUser.Id` и есть meta Guid. Он уходит пирам в `PlayerCreatePayload.Id` и во все игровые id игроков: снапшоты, `Winner`, `TargetPlayer`, `CurrentPlayer`, `SecondsLeft`, `GameCompletedRecord`.
- S4:
  - `SessionConnectionMiddleware` брал `auth.UserId` и `auth.SessionId` из первого кадра клиента, без проверки. Можно войти в любую сессию под любым юзером;
  - `ConnectionOneTimeHandle.ReadRequest` читал с `CancellationToken.None`: молчащий клиент держит сокет вечно.
- Все пути к game-сессии в B (каждому нужен тикет):
  1. `SearchMatch` → `Matchmaking.Loop` → `MatchFactory.Create` (два человека).
  2. `SearchMatch` → `Loop` по таймеру → `MatchFactory.CreateWithBot`.
  3. Команда `Create`: `MatchFactory.Create` с одним участником. Клиент её не вызывает.
  4. Команда `CreateWithBot`: GameMock и agent-play.
  5. `SearchLobby` → `LobbyFactory`. Клиент его не вызывает, но путь живой.
  6. Реванш: game-сервер сам создаёт сессию (`MatchHandle`) и раньше слал `RematchContexts.Success` всем.
- Бот подключается без сокета: `UserFactory.CreateBot` внутри сессии. Тикет и место ему не нужны, место резервируется только человеку.

## Что сделано

### Shared (протокол)

- `SharedBackendSocketAuth`. Раньше там была только константа query-ключа, теперь это первый кадр meta-сокета:
  - `Request { GuestId, GuestSecret }`;
  - `Response { Status, UserId, IssuedSecret }`;
  - `AuthStatus { Ok, InvalidCredentials, InvalidRequest }`;
  - `RejectedCloseCode = 4003`;
  - union-теги 76/77.
  - `UserIdQueryKey` удалён.
- `SharedSessionAuth.Request { Ticket }`, `Response { Status }`, enum `SessionAuthStatus { Ok, InvalidTicket, Expired, NoReservation, Closed }`. Теги 59/60 те же.
- `SharedSessionPlayer`:
  - `LocalUpdate { Index, Id }`;
  - `RemoteUpdate { Index, Id }` (раньше `BackendId`). `Id` — id юзера в этой сессии.
- `Ticket` добавлен в `MatchResult`, `LobbyResult`, `RematchContexts.Success`.

### Бэкенд, S3

- `Meta/Users/Auth/GuestSecrets.cs` как в A: 32 случайных байта, base64url; хранится SHA-256, сравнение через `FixedTimeEquals`.
- `UserAuthState`:
  - новое поле `GuestSecretHash` и событие `GuestSecretIssued` в потоке `user_auth`;
  - `GetOverview`;
  - `IssueGuestSecret` (один раз, только существующему юзеру).
- `UserFactory`:
  - `CreateGuest`;
  - `ResolveGuest(id, secret)`. Неизвестный id получает нового гостя (стёртая БД). У юзера без секрета и при неверном секрете — `null`, то есть отказ;
  - `Resolve(Guid?)` и неиспользуемый `Create(options)` удалены;
  - проекции читаются в той же транзакции, как было в B.
- `MetaGateway/UserFlow/Connection/MetaSocketAuth.cs`. Только гость: пусто — новый гость, id+секрет — вход, одно без другого — `InvalidRequest`.
- `BackendConnectionMiddleware`:
  - после проверки протокола (N7) принимает сокет, читает первый кадр с таймаутом 10 с, отвечает;
  - отказ: ответ со статусом, затем close 4003;
  - сбой кластера: close `InternalServerError`;
  - query `userId` больше не читается.
- Пиры не видят meta Guid:
  - `IUser.Id` — id в сессии, новый Guid в каждой сессии, один на meta-юзера внутри сессии (`UserFactory.GetSessionId`);
  - `IUser.BackendId` — meta id. Нужен только для meta: колода в `PlayerFactory`, live-данные консоли в `SessionsCollection`, бот реванша;
  - `GameFlow` переводит id на границе с грейном матча через `MatchUserIds`: `Setup`, `OnComplete(winner, stats)`, `MatchCompletionSummary` обратно в id сессии. Карта снимается на старте, поэтому ушедший игрок не теряется.

### Бэкенд, S4

- `Common/Network/SessionTickets.cs`, как в A:
  - формат `v1.payload.mac`, HMAC-SHA256;
  - живёт 60 с;
  - nonce одноразовый.
- `Orchestration/Extensions/SessionTicketsSetup.cs`, как в A:
  - ключ `SESSION_TICKET_SECRET`, base64, не меньше 32 байт;
  - пустой ключ в Development — dev-ключ;
  - вне Development пустой, короткий, не base64 или dev-ключ роняют старт.
- `ConnectionOneTimeHandle.ReadRequest(CancellationToken)`: таймаут handshake.
- `Game/Session/Root/SessionReservations.cs` (новое, в B нет `SessionAdmission`):
  - место живёт `JoinTimeout` (30 с) и используется один раз;
  - `ISession.Reserve` и `TryAccept`.
- Резервы:
  - `SessionFactory.CreateMatch(options, participants)`;
  - `CreateMatchWithBot(botId, options, participant)`: только человек;
  - `SessionSearch.GetOrCreateLobby(userId)`.
  - В `MatchPayloads` добавлены `Participants` и `Participant`.
- Выдача тикетов:
  - `MatchFactory` выдаёт по тикету каждому участнику (`Create`, `CreateWithBot`);
  - `LobbyFactory` — тикет юзеру;
  - `MatchHandle` (реванш) — каждому человеку свой `RematchContexts.Success` с тикетом game-сервера.
- `GameGateway/SessionGate.cs` (по A, без `OnJoinFailed`) проверяет по порядку:
  1. подпись, срок и nonce тикета;
  2. сессия есть и жива (иначе `Closed`);
  3. место (иначе `NoReservation`).
- `SessionConnectionMiddleware`:
  - таймаут handshake 10 с;
  - на отказе отвечает статусом и закрывает с `PolicyViolation`;
  - `Close` с таймаутом 5 с.
- Регистрация: `MetaGateway/Program.cs` `AddSessionTickets()`, `GameGateway/Program.cs` `AddSessionGate()`, `MetaSocketAuth` в `AddUserFlow`.
- Конфиг:
  - compose: `SESSION_TICKET_SECRET: ${SESSION_TICKET_SECRET:?}` у meta и game;
  - Aspire передаёт meta и game dev-ключ явно (переопределяется `SessionTicketSecret` в appsettings);
  - обновлены `.env.example`, `COOLIFY.md`, `.claude/docs/DEPLOY.md` (новый раздел "Guest secret (S3) and session tickets (S4)").

### Клиент

- `Meta/Connection/Authentication.cs`:
  - запись гостя `{Id, Secret}` в JSON под ключом `Auth.Guest.<Local|Production>:<scope>`. Ключ разделён по окружению бэкенда (замечание N12): в редакторе локальный гость не перезаписывает прод;
  - `PlayerPrefs.Save()` сразу после записи;
  - `LoadLegacyUserId` только для сообщения в лог.
- `BackendOptions.Target`: в билде всегда Production, в редакторе — выбранное окружение.
- `LocalStorageKeys`: `Guest`, `LegacyUserId` (бывший `UserId`) и `Get(name, environment)`.
- `MetaBackend.Connect(lifetime, request)` отправляет первый кадр и возвращает ответ. На отказ бросает `MetaAuthRejectedException`.
- `MetaLoop`:
  - сохраняет `IssuedSecret` до всего остального;
  - сверяет профиль с `response.UserId`;
  - на `MetaAuthRejectedException` не повторяет попытки, пишет `LogError`;
  - reconnect идёт с сохранёнными данными гостя;
  - старый id без секрета пишется в лог warning-ом, и создаётся новый гость.
- Сессия:
  - `NetworkSession.Start(lifetime, serverUrl, ticket)`;
  - `SessionConnection.Connect` с тикетом, на отказ бросает исключение;
  - `INetworkUser.BackendId` → `Id` (id в сессии), `LocalUserUpdateCommand` берёт `context.Id`;
  - `LocalUserId` удалён.
- `GamePlayLoop`: тикет из `MatchResult`, зависимость `IProfile` убрана.
- `GameLoop` и `RematchAwaiter` / `SessionData` передают тикет реванша.
- `PlayerEntityExtensions`, `EntityDestroyedCommand`: `owner.Id`.
- Editor `UserTab` (Project Tools > User):
  - показывает гостей обоих окружений и старый ключ;
  - Copy копирует только id;
  - Clear удаляет гостей обоих окружений и старый ключ.
- EditMode `ConnectionWriterTests`: `IsSuccess` → `Status`.

### Документация

`docs/obsidian/architecture/client-network.md`, `shared-protocol.md`, `game/match-flow.md`.

## Старый клиент и старый гость (legacy-окна нет, Q1)

| Кто | Что будет |
|---|---|
| Закешированный старый WebGL-клиент | 4001 по хэшу протокола (N7) раньше любого чтения, окно "Update available", перезагрузка в новый билд |
| Новый клиент на устройстве со старым `userId` / `Auth.UserId:<scope>` | Отправляет пустой запрос и получает **нового гостя**: пустой профиль, рейтинг, колоды и история с нуля. В консоли warning `[Meta] [Loop] The saved user <id> has no guest secret and cannot be entered: a new guest is created`. Старые ключи не трогаются |
| Кто-то шлёт старый id без секрета | `InvalidRequest`, close 4003 |
| Кто-то шлёт старый id с любым секретом | `InvalidCredentials`, close 4003. Секрет не выдаётся, старый аккаунт недоступен |
| `?userId=` в query | Игнорируется |
| Клиент с сохранённым гостем, которого сервер не знает (стёртая БД) | Новый гость, запись на клиенте перезаписывается |
| Клиент, чей сохранённый гость отвергнут (неверный секрет) | `LogError`, повторов нет, меню остаётся на экране загрузки. Запись не удаляется, её видно в Project Tools |
| Старый кадр game-сессии `{UserId, SessionId}` | Хэш протокола уже закрыл сокет (4001). С подделанным хэшем — `InvalidTicket` |

Старые аккаунты остаются в БД, но войти в них нельзя.

## Протокол

- `ProtocolVersion.Hash`: `522f7664226053dd` → `160117fe01eb67ed`.
- `protocol_schema.golden.txt` и `protocol_bytes.golden.txt` перезаписаны (`UPDATE_PROTOCOL_GOLDEN=1`). Изменились только перечисленные выше типы, два новых enum и теги 76/77, остальные строки побайтно те же. Клиентский `ProtocolBytesTests` читает тот же файл.

## Проверка

- `dotnet build backend/backend.slnx`: 0 ошибок. Новые предупреждения только xUnit1051 в тестах, такие же есть во всём проекте.
- Новые и затронутые тесты:
  - `SessionTicketsTests`, `ConnectionOneTimeHandleTests`, `SessionGateTests`, `SessionUserIdsTests`, `SessionConnectionMiddlewareTests`, `MetaSocketHandshakeTests`, `MetaSocketAuthTests` (на Orleans-кластере), `SessionCloseTests`, `SessionRunTests`, `MatchmakingTests`: 70/70;
  - протокол: 589/589.
- Полный прогон: 1885/1885, пропущенных 0. Docker был доступен.
- Флак: `SessionCloseTests.MatchWithBot_HumanLeaves_SessionCloses(isHalfOpen: True)` один раз упал по таймауту 5 с в групповом прогоне. Повторы зелёные: 5/5 класса отдельно, 4/4 той же группы. Тест на keep-alive из N2, путь abort я не менял.
- Клиент не собирался: в worktree нет `.csproj`.

## Что проверить в редакторе

1. Компиляция. Генератор контейнера должен принять:
   - `Authentication(BackendOptions)` в Meta-скоупе;
   - `GamePlayLoop` без `IProfile`;
   - `UserTab`.
2. EditMode: `Internal.Tests.ConnectionWriterTests`, `NetworkConnectionTests`, `ProtocolBytesTests` (golden новый).
3. Живые сценарии на свежем Aspire из этой ветки. Бэкенд пересобрать: хэш новый.
   1. **Новый гость.** Project Tools > User > Clear, затем Play.
      - Клиент: `Connecting to backend as: new guest`.
      - Meta: `[User] [Auth] Guest secret issued to <id>`.
      - В UserTab появился `Guest Local` с этим id.
   2. **Повторный вход тем же гостем.** Stop, затем Play.
      - Клиент: `Connecting to backend as: guest <id>`, тот же профиль.
      - Новой выдачи секрета нет.
   3. **Чужой userId без секрета.** Временно в `MetaLoop.CreateAuthRequest` вернуть `new Request { GuestId = <id другого юзера> }`, потом откатить.
      - Клиент: `LogError ... refused ... InvalidRequest`, повторов раз в 2 с нет.
      - Meta: `[Meta] [Auth] Refused: InvalidRequest` и `Auth rejected`.
      - С `GuestSecret = "x"`: `InvalidCredentials`.
   4. **Game-сессия без тикета или с чужим тикетом.** Временно в `SessionConnection.Connect` подставить `Ticket = "v1.AAAA.AAAA"`, потом откатить.
      - Клиент: исключение `The session refused the entry: InvalidTicket`.
      - Game: `[Game] Connection rejected: InvalidTicket`.
      - Чужой валидный тикет (`NoReservation`) вживую не воспроизвести без второго клиента. Его покрывают `SessionGateTests` и `SessionConnectionMiddlewareTests`.
   5. **Матч с ботом.**
      - GameMock и меню (Search → бот по таймеру): ходы и победа/поражение на экране результатов. Рейтинг меняется: id переводятся в `MatchUserIds`.
      - Game: `[Session] User created: 1 <metaId> <sessionId>` (id разные), `[Game] User connected ... <SessionId>`.
      - Реванш с ботом: новый тикет от game-сервера, вход проходит.
   6. **Два клиента (MPPM).** Матч, и пиры видят друг друга. В game-логе id в сессии не совпадают с meta id.
   7. **Окружение.** Project Tools > Options: Environment = Production, затем назад на Local. Записи `Guest Production` и `Guest Local` разные, локальный гость не затёр прод.

## Деплой

- Coolify: `SESSION_TICKET_SECRET` = `openssl rand -base64 48`, пометить Secret. Без него compose не стартует. Значение одно для meta и game (compose берёт одну переменную).
- `GUEST_LEGACY_LOGIN_UNTIL` не нужен.
- Миграции БД нет. Новое поле снапшота `user_auth` у старых юзеров пустое, новое событие `GuestSecretIssued`. Бэкап БД перед выкаткой всё равно сделать.
- Порядок: бэкенд, сразу за ним WebGL (`deploy-webgl.sh`), протокольный релиз вместе с N4/N6/N7/N10. Между шагами все клиенты видят окно обновления.
- Что увидят существующие игроки: после обновления каждый станет новым гостем, прогресс старого аккаунта недоступен (решение Q1). В редакторе так же: прежний `Auth.UserId:<hash>` больше не используется.
- Мониторинг:
  - meta: `Guest secret issued to`, `[Meta] [Auth] Refused`, `Auth rejected`;
  - game: `Connection rejected: <Status>`. После деплоя ожидаются только `Closed`/`NoReservation` от опоздавших.
- Откат: старый бэкенд читает новые снапшоты (лишнее поле игнорируется), старый клиент находит юзера по `Auth.UserId:<scope>`. У гостей, созданных после S3, этого ключа нет, старый клиент заведёт им нового юзера.
- Смена `SESSION_TICKET_SECRET` ломает только тикеты последней минуты.

## Отличия от A

- Нет Telegram, legacy-окна (`GUEST_LEGACY_LOGIN_UNTIL`, `LegacyUserId`, `GuestLoginOptions`) и `TelegramLinksBackfill`. `AuthStatus` состоит из трёх значений, `InvalidRequest = 2`.
- Теги `SharedBackendSocketAuth` 76/77 (в A 12/13). `SharedSessionAuth` остался на 59/60.
- В B нет `SessionAdmission`, поэтому сделан минимальный `SessionReservations`: срок и одноразовость, без seal и подсчёта закрытия. Сессию закрывает `JoinTimeout` N2. `SessionGate.Join` без `OnJoinFailed`: место, потраченное на упавший ответ, не возвращается, матч закроется по таймауту.
- Тикеты выдаются в matchmaking B: `MatchFactory`, `LobbyFactory` и `MatchHandle` (реванш на game-стороне, тикет на каждого человека). `SessionTicketRequest` и `LobbyResult` как ответ команды (F2.15) не переносились, это N10.
- Пиры: в A id из сессии убраны совсем (игроки по индексу). В B игровой протокол везде держит Guid игрока, поэтому введён id в сессии (`IUser.Id`) и `BackendId` для meta, а на границе с грейном матча id переводятся.
- `ResolvedUser` сохранил `Projections`: handshake в B всё ещё читает проекции (см. N4).
- Ключ гостя разделён по окружению: `Auth.Guest.<env>:<scope>`, в A `Auth.Guest:<scope>`.
- Клиент: отказ превращается в `MetaAuthRejectedException`, `MetaLoop` перестаёт повторять. В B есть цикл повторов и reconnect, которых в A нет.
- Aspire явно передаёт dev-ключ.
- `ManualTimeProvider` упрощён (без таймеров).
- Тесты:
  - `SessionGateTests` написаны под B;
  - `MetaSocketAuthTests` без legacy/Telegram, добавлен тест "юзер без секрета";
  - новые `MetaSocketHandshakeTests` (отказ 4003, query userId игнорируется), `SessionUserIdsTests`;
  - `GuestLoginOptionsTests`, `TelegramLinksBackfillTests`, `SessionRequester*Tests` не переносились.

## Риски и заметки

- Meta Guid соперника ещё уходит клиенту через историю матчей: `SharedBackendUser.Match.Participants` в ответе `MatchHistory`. Это канал meta, а не пиров в матче. После S3 по Guid нельзя войти, поэтому оставил. Если нужно, убрать отдельной задачей: клиент использует только `Winner == userId`.
- Команда `Create` (матч на одного, ожидает двоих) резервирует место только создателю. Второй войти не может, сессия закроется по `JoinTimeout`. Клиент эту команду не вызывает.
- Каждая следующая задача волны 4 (N10) обязана снова перезаписать golden и `Hash`.
