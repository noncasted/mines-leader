# N8. Стабильные id свойств и сервисов: handoff

Статус: review (бэкенд проверен; клиент проверен чтением, компиляция в редакторе за оркестратором).
Источник: peace-data-reborn `875b9d4a7` (F3.4, F3.5), только `StableHash` и проверка дубликатов в `NetworkEntity`.

## Шаг 0: баг

Подтверждён чтением кода, но сейчас он **латентный**: матч не сломан.

- Клиент, `NetworkEntitiesExtensions.RegisterProperty<T>`: id = `typeof(T).FullName.GetHashCode()`. На Unity 7000 (CoreCLR) хэш свой в каждом процессе, у двух клиентов id разошлись бы. Но `RegisterProperty` в B **никто не вызывает**: у сущностей B нет сетевых свойств (`Send` шлёт пустой список, `FillProperties` сверяет 0 == 0). Всё состояние матча идёт через сервисы и события.
- Сервер сам id свойств сущностей не считает: `EntityFactory.Create(request)` берёт id из запроса клиента, `EntityBuilder.AddProperty` не используется.
- Сервисы: свойства на клиенте нумеруются по порядку (`WithProperty`, 1, 2, ...), id сервиса клиент берёт из ответа сервера (`GetResponse.Id`). Сервер считает id как `key.GetHashCode()` (в `Service` и ещё раз в `ServiceFactory.GetOrCreate`). Оба вызова в одном процессе силоса, поэтому значения совпадают, и сессия живёт в одном процессе. На клиенте `NetworkServiceBuilder._id = key.GetHashCode()` — мёртвое поле.
- Итог: на текущем коде id между процессами нигде не сравниваются, матч (с ботом и PvP) из-за CoreCLR не ломается. Сломается при первом `RegisterProperty` на сущности или если id сервиса начнут считать на клиенте. Фикс превентивный и делает id воспроизводимыми (логи, тесты).
- Тест `ServiceId_IsStableHashOfKey` на старом коде падает (`string.GetHashCode` в .NET 10 рандомизирован).

## Правки

- `shared/Protocol/StableHash.cs` (+ `.meta`, новый GUID): FNV-1a по UTF-16, код A без изменений. `shared` подключён к Unity одной asmdef `Shared.Game` (вся папка), `Internal` уже ссылается на неё (`using Shared` в `NetworkServiceExtensions`).
- `client/Assets/Common/Internal/Runtime/Tools/Network/Entities/NetworkEntitiesExtensions.cs`: `StableHash.Get(typeof(T).FullName)`.
- `client/Assets/Common/Internal/Runtime/Tools/Network/Entities/NetworkEntity.cs`: два свойства с одним id — `InvalidOperationException` с именами обоих типов (как в A) вместо `ToDictionary`.
- `client/Assets/Common/Internal/Runtime/Tools/Network/Services/NetworkServiceExtensions.cs`: удалено мёртвое поле `_id = key.GetHashCode()`.
- `backend/Game/Session/Objects/Services/Service.cs`: `Id = Shared.StableHash.Get(key)` (полное имя: в бэкенде есть `Orleans.StableHash`).
- `backend/Game/Session/Objects/Services/ServiceFactory.cs`: убран второй расчёт id через `GetHashCode`; свойства привязываются к сервису, `Construct` делает `Service.Setup` (как и раньше, он перезаписывал тот же вызов).
- `backend/Tools/Tests/Protocol/StableHashTests.cs`: известные значения из A + id сервиса = `StableHash(key)`.

Клиентскую сеть другого исполнителя (ConnectionWriter, NetworkConnection, SessionConnection, JsWebSocket, Meta/*) не трогал.

## Проверка

- `dotnet build backend/backend.slnx` и `Tests.csproj`: 0 ошибок.
- `Tests --filter-class "*StableHash*"`: 7/7.
- Полный прогон: 1182/1184, падают только 2 `Tests.Meta.UserDeckTests` (известное, B13). Пропущенных нет.
- Клиент: не компилировался (нет .csproj в worktree, редактор занят). Нужно: компиляция в редакторе после переноса.

## Протокол и выкатка

Значение `Id` сервиса на проводе меняется (`GetResponse.Id`, `ObjectId` в `PropertyUpdate` и `SetProperty` для сервисов). Клиент берёт id у сервера, поэтому старый клиент с новым сервером работает. Но правило этапа: бэкенд и клиент выкатывать вместе. Формат сообщений не менялся.

## Отличия от A

- Не перенесены `SharedLobbyService` / `SharedLocationService` и правки лобби/локации: это игровой код A, в B этих сервисов нет.
- В A клиентский `StableHash` уже был и переехал в shared (тот же GUID). В B его не было: файл новый, GUID новый.
- В A свойство сущности — класс `EntityNetworkProperty<T>`, в B осталось `RegisterProperty<T>` с параметром `id`.
- В A id сервиса после F2.13 сдвигается в свой диапазон (N9). Здесь только `StableHash`, диапазон — задача N9.
- Добавлены правки, которых в коммите A нет: сервер `Service`/`ServiceFactory` (в A это было сделано раньше) и удаление мёртвого `_id` на клиенте.

## Для оркестратора

- `client/Assets/Common/Internal/Runtime/Tools/LocalStorage/LocalStorageKeys.cs` (N12, основное дерево) содержит свою копию FNV-1a. После переноса N8 её можно заменить на `Shared.StableHash.Get` (значения те же). Не правил: файл другого исполнителя.
