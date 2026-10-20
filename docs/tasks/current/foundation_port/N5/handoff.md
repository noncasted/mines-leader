# N5 + N12: handoff

Статус: review. Правки в основном дереве, не закоммичены. Компиляция в редакторе (с N3, N8) чистая, EditMode 9/9, живые проверки — см. "Результаты в редакторе".

## Шаг 0: подтверждение (чтением кода)

N5:
- `ConnectionWriter.ForceSendAll` перебирает `_writeQueue`, но не очищает её. `Loop` работает на lifetime соединения (а не на `_lifetime`, который гасит `ForceSendAll`), поэтому на следующем тике шлёт те же сообщения второй раз. Если `Loop` забирает сообщение во время `await Send`, список меняется под `foreach` (InvalidOperationException). Тест `ForceSendAll_LoopDoesNotSendQueueAgain` до фикса даёт 2 отправки.
- `NetworkConnection.Run`: writer и проброс `Closed` подписываются после `await Connect()`. `DefaultWebSocket.Connect` сам запускает `Receive` на главном потоке. Если сокет оборвался между апгрейдом и следующим кадром, первый `ReceiveAsync` падает синхронно, и `NotifyClosed` вызывает `Closed` до возврата из `Connect`, когда подписчиков ещё нет. Последствия:
  - writer не закрыт, запросы ждут 20 с;
  - `SessionConnection` после таймаута получает `null` и падает с NRE на `response.IsSuccess`;
  - meta `Authorize` ждёт `IsProjectionsReceived` без таймаута, то есть вечно: меню висит на загрузке.
- `JsWebSocket.Connect` (WebGL) ждёт `_isConnected || _isDisposed`. При отказе соединения (onerror + onclose без onopen) или закрытии до следующего кадра ожидание не кончается никогда. В A это исправлено в `d4ebdac91`/`66053c89d`, в списке источников задачи этого фикса не было. Без него переподключение на WebGL при лежащем сервере зависает.
- Переподключения meta в B нет: после обрыва сокета все запросы меню получают `null` по таймауту, результат поиска не приходит.

N12:
- `Authentication.GetKey` в редакторе: `userId:{Application.dataPath.GetHashCode()}`. На CoreCLR (Unity 7000) `string.GetHashCode` рандомизирован на процесс, поэтому после каждого запуска редактора создаётся новый юзер. `Save` дополнительно пишет `userId`, но `Load` его не читает.
- Нет `PlayerPrefs.Save()` в `Authentication.Save` и `ItchSaves.Save`: на WebGL значение может не дожить до закрытия вкладки.
- `UserTab` (Project Tools) читает те же неверные ключи.

## Что сделано

N5, сеть (`client/Assets/Common/Internal/Runtime/Tools/Network`):
- `ConnectionWriter` (по `64d8e14ed`, `57fd10100`):
  - `ForceSendAll` забирает очередь (копия + `Clear`);
  - `IsClosed` добавлен в `IConnectionWriter`; true, если writer не запускался, соединение потеряно или lifetime кончился;
  - `Close()` публичный, `OnClosed` вызывает его;
  - `Run` можно вызвать повторно после обрыва: сбрасывает `_isClosed` и очередь. Если предыдущее соединение живо, бросает исключение;
  - `WriteRequest` на закрытом соединении сразу возвращает `null` с LogError (раньше ждал 20 с). Контракт B "null при неудаче" сохранён, исключения — это N4.
- `NetworkConnection.Run`: writer и `Closed` подписываются до `Connect`. `Connect` ограничен 15 с (`ConnectTimeout`): найдено вживую — при упавшем MetaGateway прокси Aspire (dcp) принимает TCP и молчит, `ClientWebSocket.ConnectAsync` висел вечно. Если сокет всё же откроется после таймаута, он живёт до конца lifetime вызывающего (writer уже закрыт). Если `Connect` упал (и lifetime не завершён), writer закрывается и бросается `ConnectionFailedException`.
- `JsWebSocket`: `OnClose` ставит `_isClosed`/`_closeCode`, `Connect` ждёт и его, а затем бросает исключение.
- `SessionConnection`: `null` вместо ответа авторизации — понятное исключение вместо NRE.

N5, переподключение meta (по образцу `336dbcb59`, под модель B):
- `IMetaConnection.EnsureConnected(lifetime)` реализован в `MetaLoop` и зарегистрирован в `MetaScopeExtensions`:
  - если `Connection.Writer.IsClosed == false`, ничего не делает;
  - иначе один общий reconnect на всех вызывающих (`UniTaskCompletionSource`). Старый lifetime соединения гасится, `MetaBackend.Connect` вызывается с тем же `userId` в query (текущий протокол, бэкенд не менялся), затем ожидается свежая `ProfileProjection`. Сверка `profile.Id == _userId`: при несовпадении исключение, потому что сервер завёл бы нового юзера;
  - обрыв (`Closed`), неоткрытый сокет или отсутствие проекций 20 с: соединение гасится, исключение уходит всем ожидающим, следующий вызов пробует заново;
  - вызывающий может бросить ожидание (`AttachExternalCancellation`), connect при этом продолжается.
- Первый вход (`Authorize`):
  - ждёт проекции в гонке с обрывом и с таймаутом 20 с, а не вечно;
  - неудачная попытка гасит своё соединение;
  - `Connect` повторяет вход каждые 2 с, пока он не удастся или не кончится scope. Меню всё это время стоит на загрузочном экране, в лог пишется предупреждение. В A меню открывалось с лежащей мета. В B меню ждёт `IsReady`, поэтому без повтора клиент навсегда оставался на загрузке.
- Вызовы: `Matchmaking.SearchGame/CreateGame/CreateGameWithBot/SearchLobby` сначала вызывают `EnsureConnected`, по аналогии с `MenuLobbyEntry`/`OpenLobbies` в A.
- `MenuPlay.Search`: если `EnsureConnected` упал, экран поиска возвращается в idle, исключение пробрасывается дальше (громко). Следующее нажатие пробует снова. Плашки `ConnectionLost`, как в A, в B нет.

N12 (только PlayerPrefs-хунки `f6f6fd317`, без гостевого секрета):
- Новый `Internal/Runtime/Tools/LocalStorage/LocalStorageKeys.cs`: ключ `<name>:<scope>`. В редакторе scope — FNV-1a от `Application.dataPath` (`Shared.StableHash` из N8), в билде scope — `player`.
- `Authentication`: ключ `Auth.UserId:<scope>`, после записи вызывается `PlayerPrefs.Save()`. В билде, если нового ключа нет, читается legacy-ключ `userId`, и он не удаляется (на случай отката). В редакторе миграции нет: юзер редактора один раз создаётся заново, как в A.
- `ItchSaves.Save`: добавлен `PlayerPrefs.Save()`.
- `UserTab`: одна строка с новым ключом, Clear удаляет его.

Тесты (`client/Assets/Common/Internal/Tests/Editor/Network/`, в asmdef `Internal.Tests` добавлены UniTask, Shared.Game и MemoryPack.Core.dll):
- `ConnectionWriterTests` (7):
  - loop отправляет;
  - `ForceSendAll` не дублирует;
  - после обрыва pending и следующий запрос сразу получают `null`;
  - ответ доходит;
  - незапущенный writer закрыт;
  - повторный `Run` после обрыва шлёт в новый сокет;
  - повторный `Run` при живом соединении бросает.
- `NetworkConnectionTests` (2, по A):
  - сырой TCP-сервер обрывает поток сразу после апгрейда: `Closed` поднят, `IsClosed`, запрос сразу `null`;
  - connect refused: `ConnectionFailedException`, `IsClosed`.
- `TestUnions`: регистрирует union `INetworkContext` в том же порядке, что `Flow.Loop.UnionInitializer`, иначе writer не сериализует.

## Проверка

- `dotnet build client/client.sln` зелёный, 0 ошибок. Новых предупреждений в изменённых файлах нет. `.csproj` перегенерировал работающий редактор соседней сессии (auto-refresh подхватил новые файлы), я его не трогал.
- Не сделано, нужен редактор:
  1. EditMode: `Internal.Tests.ConnectionWriterTests`, `Internal.Tests.NetworkConnectionTests`. Тест с сырым TCP в A проходил на той же `DefaultWebSocket`. Если на 7000 обрыв придёт не `WebSocketException`, а другим исключением, `DefaultWebSocket` сделает `Debug.LogException`, и тест упадёт на неожиданном логе.
  2. Живая проверка: остановить backend в момент connect. Ожидается warning о повторе, после старта backend меню открывается. Без перезапуска клиента: убить/перезапустить MetaGateway в меню → Play → meta переподключается тем же юзером, поиск идёт.
  3. N12: перезапуск редактора → тот же юзер (ключ `Auth.UserId:<hash>` в Project Tools → User); WebGL: перезагрузка вкладки → тот же юзер.

## Отличия от A

- Нет auth-ответа и гостевого секрета (S3): переподключение идёт по `userId` в query, проверка личности — по свежей `ProfileProjection`.
- Writer B возвращает `null`, а не бросает исключения (N4 не перенесён). Поэтому на закрытом соединении `null` приходит сразу, а не `OperationCanceledException`.
- Нет `GamePlayLoop` reconnect / `MatchReconnect` / `GameEndTransition.Reconnect` (это N13). Хунк `GamePlayLoop` из `57fd10100` не перенесён: вход в матч при неоткрытом сокете получает исключение, а не повтор.
- Первый вход в B повторяется каждые 2 с (в A меню открывалось без meta). `EnsureConnected` вызывается из `Matchmaking`, а не из `MenuLobbyEntry`/`OpenLobbies` (их в B нет).
- `JsWebSocket`: перенесено только окончание `Connect` при закрытии (из `d4ebdac91`), без `Drop`/`SocketCloseInfo`.
- `LocalStorageKeys`: ключ `Auth.UserId` (голый Guid), а не `Auth.Guest` (JSON с секретом). S3 добавит свой ключ, конфликта форматов не будет. `CharacterSelection` в B нет.
- Документация (`NETWORK.md`, `CLAUDE_MISTAKES` F36) не переносилась: в `.claude/docs` B нет `NETWORK.md`.

## Зависимости и риски

- **N3 нужен для корректного переподключения.** `ConnectedUsers.Add` подписывает `_entries.Remove(session.UserId)` на lifetime сессии. При переподключении, когда сервер ещё считает старый сокет живым, `UserConnectionEntryPoint.OnConnected` делает `ForceDisconnect` старого, и его lifetime удаляет запись **новой** сессии. Тогда `Matchmaking` (бэкенд) по `IsConnected` выкидывает юзера из поиска. Протокол и бэкенд я не менял, это фикс N3 (`030a4862d`).
- Обрыв meta во время уже идущего поиска (после отправки запроса) по-прежнему оставляет экран поиска ждать результат (его можно отменить кнопкой). Ожидание результата не завязано на `Closed`.
- Профиль-сверка при reconnect: если сервер не нашёл юзера и завёл нового, клиент получит исключение на каждой попытке, нужен перезапуск. Это громкий отказ, а не тихая подмена юзера.

## Результаты в редакторе (2026-10-21)

- Компиляция с N3 и N8: 0 ошибок. Промежуточно были ошибки в чужом незакоммиченном рефакторинге `GamePlay/Cards` (`CardPointerEvents`), потом исчезли.
- EditMode `ConnectionWriterTests` + `NetworkConnectionTests`: 9/9 (до и после N8). Полный `Internal.Tests` не прогонялся: `ContainerBenchmarkTests` долгие, MCP отваливается.
- Живые (локальный Aspire из основного дерева, `GameMock` в `Game_Field`, `_environment` временно 0, возвращён в 1):
  - вход + матч с ботом: meta → `CreateGameWithBot` → сессия, `Success = True`, ход переходит к игроку; выход из Play без ошибок;
  - повторный Play: вход тем же юзером (`Auth.UserId:<hash>`); перезапуск самого редактора не проверялся, WebGL не проверялся;
  - MetaGateway убит во время матча: `Connection lost` warning, матч продолжается, ошибок нет;
  - Play при лежащем MetaGateway: попытки каждые 2 с (таймаут 15 с через прокси, затем refused после остановки Aspire); после повторного старта Aspire клиент сам вошёл и начал матч;
  - `EnsureConnected` (переподключение из меню) вживую не проверен: нужен сценарий через сцену меню, а сцены грязные от соседней сессии и переключать их нельзя. Покрыт только чтением кода;
  - закрытая game-сессия (N3, `IsSuccess=false`) вживую не воспроизводилась: `SessionConnection` бросает "Authentication failed" (ответ читается раньше Close-кадра), `null` — отдельное исключение.
- Замечено: ключ юзера не разведён по окружению (Production/Local). Id прод-юзера, отправленный в локальный бэкенд, создаёт нового локального юзера и перезаписывает ключ. Так было и до правки.
