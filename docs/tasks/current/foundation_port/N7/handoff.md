# N7. Хэш протокола: handoff

Статус: review. Бэкенд проверен (сборка, полный прогон). Клиент проверен только чтением: компиляцию, EditMode и живую проверку окна делает оркестратор.
Источник: peace-data-reborn `d4ebdac91` (F3.2) и `56d715397` (F3.2 fix, только клиентский `JsWebSocket`).

## Шаг 0: баг

Подтверждён чтением кода. В B нет `ProtocolVersion`, клиент ничего не сообщает о своей версии. `BackendConnectionMiddleware` и `SessionConnectionMiddleware` принимают сокет любого клиента. Закешированный WebGL-клиент после смены протокола разбирает чужие байты молча. Meta-клиент (N5, `MetaLoop.Connect`) при этом повторял бы подключение каждые 2 с без конца.

## Что сделано

- `shared/Protocol/ProtocolVersion.cs`: `Hash`, `QueryKey = "protocol"`, `ClientOutdatedCloseCode = 4001`, `ClientOutdatedReason`. Файл как в A, хэш свой.
- Хэш — первые 16 hex SHA-256 от `ProtocolSchema.BuildText()` (union-теги, члены всех `[MemoryPackable]` типов shared, значения enum). Схема лежит в `backend/Tools/Tests/Protocol/protocol_schema.golden.txt` (576 строк, снята на HEAD 13a72c03), значение `Hash = "ddae8a7e3afb4a39"`.
- `ProtocolSchemaTests`: добавлены `Schema_MatchesGolden` (`UPDATE_PROTOCOL_GOLDEN=1` перезаписывает golden) и `ProtocolHash_MatchesSchema`. Тест падает, если хэш не обновили. Проверено: с заглушкой `0000000000000000` тест красный и подсказывает правильное значение. Пробное поле `int Probe` в `SharedSessionAuth.Request` (потом удалено) роняет оба теста.
- Бэкенд: `backend/Common/Network/ClientProtocolCheck.cs` как в A. Вызов стоит в `BackendConnectionMiddleware` после проверок готовности кластера и до чтения userId/поиска юзера, в `SessionConnectionMiddleware` — до accept и auth. Если хэш другой или его нет, сокет принимается и закрывается с кодом 4001.
- Клиент:
  - `SocketCloseInfo` (код и причина) вместо `string` в `IWebSocket.Closed` и `INetworkConnection.Closed`. `DefaultWebSocket` берёт код из Close-кадра (если кода нет — 1005, при обрыве — 1006). `JsWebSocket` передаёт код из `onclose`.
  - `NetworkConnection` добавляет `protocol=<hash>` в URL (через `?` или `&`). До коннекта подписывается на закрытие и на 4001 вызывает `IClientOutdatedNotice.Show()`.
  - `IClientOutdatedNotice { IsShown; Show(); }` и `ClientOutdatedException` (`Internal`).
  - `Global.UI.ClientOutdatedNotice` (папка `Global/UI/ClientOutdated/`) регистрируется в `AddUI` через `WithScopeLifetime`. Канвас собирается из кода по образцу A, оформление как в меню: 1920x1080 с привязкой по высоте, цвета панелей и текста из префабов меню, шрифт Ithaca-LVB75. Текст английский, как всё меню: "Update available" / "The game has been updated. This version can no longer connect." / "RELOAD". Окно прячет экран загрузки: в вебе тот лежит поверх канваса.
  - Кнопка в WebGL вызывает `ClientOutdatedNotice.jslib` → `location.reload()`. В редакторе она только пишет warning.
  - Шрифт берётся из нового поля `GlobalPublisherOptions._outdatedNoticeFont`, в `.asset` вписан guid Ithaca-LVB75. Окно может появиться до загрузки ассетов меню, поэтому шрифт должен лежать в группе Global.
  - `MetaLoop`: если в цикле первого подключения `IsShown`, повторов больше нет (warning и выход). `EnsureConnected` при `IsShown` сразу бросает `ClientOutdatedException`, переподключения нет. `ConnectionLoss` хранит `SocketCloseInfo`.
  - `JsWebSocket` (из `56d715397`, этого не было в N5): `OnError` больше не сбрасывает `_isConnected`. Раньше `onclose` после `onerror` терял обрыв. `Send` на закрытом или закрывающемся сокете сообщение выбрасывает и не кидает исключение.
- Тесты:
  - бэкенд: `ClientProtocolCheckTests` как в A, 4 теста; `SessionConnectionMiddlewareTests` теперь передаёт хэш в URL.
  - клиент: `NetworkConnectionTests.ServerClosesAsOutdated_ClosedCarriesCode_NoticeShown` (новый). Сервер на `HttpListener` закрывает сокет с 4001. Тест проверяет код 4001 в `Closed`, `Show` ровно один раз, `Writer.IsClosed`, а также что query `?userId=1&protocol=<Hash>`. `ConnectionWriterTests` и `NetworkConnectionTests` переведены на `SocketCloseInfo` и новый конструктор.
- Документация: `docs/obsidian/architecture/shared-protocol.md`, раздел "Версия протокола".

## Проверка

- `dotnet build backend/backend.slnx`: 0 ошибок.
- Новые тесты (`*ProtocolSchemaTests*`, `*ClientProtocolCheckTests*`, `*SessionConnectionMiddlewareTests*`): 9/9.
- Полный прогон: 1812/1812, пропущенных 0.
- В worktree не хватало `backend/Tools/Tests/Protocol/ProtocolValueText.cs`: без него HEAD 13a72c03 не собирается. Файл из N6 не попал в коммит и в основном дереве лежит неотслеживаемым. Скопировал его в worktree без изменений. Его нужно закоммитить (с N6 или с N7).

## Что проверить в редакторе

1. Компиляция. Генератор контейнера должен принять `ClientOutdatedNotice` в Global (`IReadOnlyLifetime` через `WithScopeLifetime`, `ILoadingScreen`) и `IClientOutdatedNotice` в конструкторах `NetworkConnection` и `MetaLoop` (meta- и session-скоупы — дети Global).
2. EditMode: `Internal.Tests.NetworkConnectionTests` (3 теста, новый — `ServerClosesAsOutdated_...`), `ConnectionWriterTests`.
3. Инспектор `GlobalPublisherOptions.asset`: поле Outdated Notice Font = Ithaca-LVB75.
4. Живая проверка окна. На свежем Aspire с этой веткой поменять только на клиенте в `NetworkConnection.cs` строку `private readonly string _protocolHash = ProtocolVersion.Hash;` на `"0000000000000000"` (правку потом откатить). Бэкенд при этом не пересобирать.
   - Play из меню: окно "Update available" поверх всего, экран загрузки скрыт, клики под окном не проходят. В консоли один warning `[Meta] [Loop] The client is outdated: the meta connection is not retried`, новых попыток раз в 2 с нет. В логе meta-шлюза `Outdated client protocol 0000000000000000, expected ddae8a7e3afb4a39`.
   - "RELOAD" в редакторе пишет warning. Выход из Play уничтожает канвас.
   - Откатить хэш: вход, матч с ботом, выход работают как раньше (query `protocol=` принимается).
   - Сценарий "деплой во время меню" (meta жив, game-шлюз уже новый) так не воспроизвести. Покрывает тот же `OnSocketClosed` в `NetworkConnection`.

## Отличия от A

- Нет `WebSocketClosedException`: в B `JsWebSocket.Connect` уже бросает `InvalidOperationException` с кодом (N5). Окно от этого не зависит: `Closed` с 4001 срабатывает в `OnClose` раньше.
- `DefaultWebSocket.Send` из `56d715397` не переносил. В B `Send` и так не сообщает обрыв (в A этот баг появился в `d4ebdac91`), код 4001 доходит из цикла приёма. Клиентский `DefaultWebSocketTests` из A не перенесён: в B он упал бы на исключении из `Send` (его ловит writer). Вместо него — тест на уровне `NetworkConnection`.
- `CommandDispatcher` (логгер) из `56d715397` к задаче не относится, не переносил.
- `IClientOutdatedNotice.IsShown` и `ClientOutdatedException` — новое для B: нужны, чтобы остановить повторы meta (в A нет reconnect-цикла).
- Окно в стиле меню B (цвета и шрифт меню, английский текст), а не в стиле чата A. Шрифт берётся через `GlobalPublisherOptions`, потому что в B нет `ChatOptions`.
- В B нет `WebPage` (A `WebPage.Reload` со снятием "Leave site?"), поэтому свой маленький `ClientOutdatedNotice.jslib`. Подтверждения ухода со страницы в B нет.
- Выделение кнопки через `EventSystem.current` не переносил: на null оно падало бы, а для мыши и тача не нужно.

## Риски и заметки

- Протокольный релиз: старые клиенты без `protocol=` получат окно только от нового бэкенда. Бэкенд и клиент выкатываются одним деплоем (overview, волна 4).
- Каждая следующая задача волны 4 (N4, S3, S4, N10) меняет схему. Ей нужно перезаписать golden (`UPDATE_PROTOCOL_GOLDEN=1`) и обновить `ProtocolVersion.Hash`.
- Ithaca-LVB75 теперь входит в зависимости группы Global. Если шрифт лежит и в группе Menu, Addressables может положить копию в бандл Global (+размер WebGL). Проверить при сборке WebGL.
- `location.reload()` возьмёт новый билд, только если `index.html` и loader не закешированы как immutable (см. память про CRC-кеш бандлов).
