# N7 fix: LogError при отправке в сокет, закрытый по 4001

Статус: review. Правка только клиентская, проверена чтением. Бэкенд, shared и протокол не менялись, `ProtocolVersion.Hash` прежний.

## Причина

Ошибку пишет `ConnectionWriter.Loop` (`Failed to send message to server`), когда `DefaultWebSocket.Send` бросает `WebSocketException` на сокете в состоянии `Closed`. Такое бывает только в редакторе и на мобильных: в WebGL `JsWebSocket.Send` на закрытом или закрывающемся сокете сообщение и так выбрасывает.

Последовательность для устаревшего клиента:

1. Шлюз (`ClientProtocolCheck.RejectOutdated` в `BackendConnectionMiddleware` и `SessionConnectionMiddleware`) принимает сокет и сразу шлёт Close 4001.
2. `NetworkConnection.Run` возвращается после `ConnectAsync`. С S3 `MetaBackend.Connect` сразу ставит в очередь writer-а первый кадр авторизации. С S4 `SessionConnection.Connect` так же ставит тикет.
3. Цикл приёма `DefaultWebSocket.Receive` читает Close-кадр и подтверждает его (`AcknowledgeClose` → `CloseOutputAsync`). После этого `ClientWebSocket.State == Closed`.
4. Writer узнаёт об обрыве только из `Closed`, а `NotifyClosed` вызывается после подтверждения и переключения на главный поток. Его очередь очищается позже.
5. Если в окне между 3 и 4 тикает цикл writer-а (опрос раз в 50 мс), `SendAsync` падает с `invalid state ('Closed')`, и writer пишет LogError.

До S3 ошибки не было, потому что авторизация шла в URL и в этом окне клиенту было нечего отправлять. Окно обновления при этом показывалось правильно, потому что код 4001 приходит из цикла приёма независимо от отправки.

## Правка

`client/Assets/Common/Internal/Runtime/Tools/Network/Sockets/DefaultWebSocket.cs`:

- Новый флаг `_isCloseReceived`. Цикл приёма, прочитав Close-кадр сервера, переходит на главный поток, ставит флаг и только потом подтверждает закрытие. `Send` тоже вызывается только с главного потока, поэтому никакая отправка не может начаться между флагом и переходом сокета в `Closed`.
- `Send` при поднятом флаге выбрасывает сообщение. Обрыв уже сообщён (или будет сообщён) через `Closed` с кодом сервера, а writer отменяет ожидающие запросы через `OperationCanceledException`. `JsWebSocket` ведёт себя так же.
- Настоящие ошибки отправки (сокет `Open`, сбой сети, `Aborted` после TCP-обрыва) по-прежнему бросают исключение и логируются writer-ом. Глушилок нет: выбрасывается только сообщение в сокет, который сервер закрыл с известным кодом.

Почему не другие варианты:

- Запрет авторизационного кадра до проверки протокола потребовал бы ответа сервера «протокол принят». Это лишний round trip и изменение протокола.
- Если сообщать `Closed` до подтверждения закрытия, обработчики обрыва (`MetaLoop` завершает lifetime соединения → `Shutdown`) начинают закрывать сокет параллельно с подтверждением.

`client/Assets/Common/Internal/Tests/Editor/Network/NetworkConnectionTests.cs`:

- `ServerClosesAsOutdated_ClosedCarriesCode_NoticeShown`: сразу после `Run` отправляется запрос (как это делают `MetaBackend` и `SessionConnection`). Ожидается `OperationCanceledException`. При гонке LogError уронит тест (Unity Test Framework падает на неожиданный LogError), но гонка не детерминирована.
- Новый `DefaultWebSocket_SendAfterServerClose_IsDropped` — детерминированная версия. Сервер закрывает с 4001, тест ждёт `Closed` (к этому моменту сокет уже `Closed`) и вызывает `Send`. До правки здесь `WebSocketException`, после правки отправка проходит молча.

## Game-сессия (S4)

Гонка та же: `SessionConnection.Connect` отправляет тикет сразу после `Run`, а `SessionConnectionMiddleware` закрывает устаревшего клиента с 4001 до auth. Правка в `DefaultWebSocket` закрывает гонку для обоих шлюзов. Отдельных правок в сессии не нужно. На 4001 `SessionConnection.Connect` получает `OperationCanceledException` (writer закрыт), исключение уходит через `NetworkSession.Start` в `GamePlayLoop.Process`. Это прежнее поведение входа в сессию, к гонке оно не относится.

## Что проверить в редакторе

1. Компиляция без ошибок.
2. EditMode `Internal.Tests.NetworkConnectionTests`: 4 теста, новый — `DefaultWebSocket_SendAfterServerClose_IsDropped`.
3. Живая проверка из N7 (`handoff.md`, пункт 4): хэш в `NetworkConnection._protocolHash` подменить на `"0000000000000000"`, затем Play. В консоли:
   - нет `Failed to send message to server`;
   - есть warning `[Network] [DefaultWebSocket] Connection lost: code=4001 ...`;
   - есть warning `[Meta] [Loop] The client is outdated: the meta connection is not retried`;
   - окно "Update available" на месте.
   Повторить запуск несколько раз: гонка плавающая. После проверки хэш откатить.
4. Обычный вход, матч с ботом, выход: без новых ошибок.
