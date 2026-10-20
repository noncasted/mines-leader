# N6. Union-теги явно: handoff

Статус: review. Бэкенд проверен. Клиент проверен только чтением: компиляцию в редакторе и EditMode `Internal.Tests.ProtocolBytesTests` запускает оркестратор.
Источник: peace-data-reborn `d4ccc67b2` (явные теги, одна регистрация), `3bdde2d39` (golden-тест байтов). Перенумерация по диапазонам из `6ceb82b0e` **не** перенесена (см. "Отличия от A").

## Шаг 0: баг

Подтверждён чтением кода. `shared/Protocol/UnionBuilder.cs` (`Build`, `types[i] = (i, _items[i])`) выдавал тег по позиции в цепочке `Add<>()`. Цепочку собирали дважды: на бэкенде (`ProjectsSetupExtensions.AddMemoryPack`) и на клиенте (`Flow.Loop.UnionInitializer`), плюс копии в тестах (`NetworkContextsSetup`, `TestUnions`). Если вставить или переставить тип в любом `Register`, теги всех следующих типов сдвигаются молча, и старый клиент разбирает не тот тип.

## Порядок работ

1. Golden байтов снят на **текущем** протоколе B, до правок `shared/`. В тот момент `ProtocolSchema` брал теги `INetworkContext`/`IEntityPayload` у записывающего `IUnionBuilder` (тот же порядок, что в `AddMemoryPack`) и регистрировал настоящий `UnionBuilder`. `protocol_bytes.golden.txt` содержит 566 строк, из них 267 union (75 `INetworkContext`, 3 `IEntityPayload`). sha256 `b1917a4d1f7af1775a5f4928f53a3af08404d39f356920f68bc2a51e0d51b932`.
2. Явные `[MemoryPackUnion(tag, typeof(T))]` на `partial interface INetworkContext` / `IEntityPayload` в файле каждой группы, теги 0..74 и 0..2 как были. Оба интерфейса теперь `[MemoryPackable]`, а не `NoGenerate`, так что формат генерирует MemoryPack. `UnionBuilder`, `AddShared*`, `Register`, `UnionInitializer`, `AddMemoryPack`, `AddSharedContexts` (там был закомментированный код), `NetworkContextsSetup` и `TestUnions` удалены.
3. Временный код в `ProtocolSchema` убран, файл совпадает с A. Golden **не перезаписывался**, после правок файл побайтно тот же (`cmp` с копией из шага 1 — совпадает).
4. Проверка "без тега или с дублем — падает":
   - тип `[MemoryPackable] : INetworkContext` без тега: `ProtocolSchemaTests.EveryImplementation_HasUnionTag` и `ProtocolBytesTests` падают (проверено пробным типом, потом удалён);
   - дубль тега (0 или 26): сборка `shared` падает с ошибкой генератора `MEMPACK012 union tag value is duplicate`;
   - дубль типа или тега между базами ловит `ProtocolSchemaTests.UnionTags_AreUniquePerBase`.

## Файлы

- shared: `Protocol/INetworkContext.cs`, `Session/IEntityPayload.cs` (атрибут и комментарий); теги в `Protocol/EmptyResponse.cs`, `Protocol/CardPreviewBundle.cs`, `Backend/SharedBackendProjection.cs`, `Backend/SharedBackendUser.cs`, `Backend/SharedMatchmaking.cs`, `Configs/{CardConfigOptions,InGameAchievementOptions,MatchMakingOptions}.cs`, `Game/{SharedGameAction,MatchActionContexts,MenuPlayerPayload}.cs`, `Game/Agent/*.cs`, `Game/Cards/CardCreatePayload.cs`, `Game/Player/PlayerCreatePayload.cs`, `Game/Cheats/GameCheatContexts.cs`, `Game/Context/RematchContexts.cs`, `Game/Snapshots/SharedMoveSnapshot.cs`, `Session/SharedSession{Auth,Entity,Object,Player,Service}.cs`. Удалены `Protocol/UnionBuilder.cs`, `Game/SharedGameExtensions.cs`, `Backend/SharedBackendExtensions.cs`, `Session/SharedSessionExtensions.cs` (вместе с `.meta`).
- backend: `Orchestration/Extensions/ProjectsSetupExtensions.cs` (−`AddMemoryPack`), `Orchestration/Extensions/ServiceDefaultsExtensions.cs` и `Common/Extensions/SharedExtensions.cs` (−`AddSharedContexts`).
- backend tests: новые `Tools/Tests/Protocol/ProtocolSchema.cs` (как в A), `ProtocolSchemaTests.cs` (только два теста тегов), `SharedSerializationTests.cs` (как в A, round-trip каждого типа), `ProtocolBytesTests.cs`, `protocol_bytes.golden.txt`. Удалён `Network/NetworkContextsSetup.cs`, его вызовы убраны из `SessionRunTests`, `SessionCloseTests`, `ConnectionWriterTests`, `ConnectionReaderTests`, `SessionConnectionMiddlewareTests`.
- client: удалён `Common/Flow/Loop/UnionInitializer.cs`, вызовы убраны из `Flow/Startup/GameStartup.cs` и `Flow/Mocks/MockBase.cs`. Удалён `Internal/Tests/Editor/Network/TestUnions.cs`, `[OneTimeSetUp]` убран из `ConnectionWriterTests` и `NetworkConnectionTests`. Новый `Internal/Tests/Editor/ProtocolBytesTests.cs` + `.meta`.
- docs: `docs/obsidian/architecture/shared-protocol.md`, `client-common.md`.

Golden один: клиентский тест читает `backend/Tools/Tests/Protocol/protocol_bytes.golden.txt` через `Application.dataPath/../../backend/...`, копии нет.

## Проверка

- `dotnet build backend/backend.slnx`: 0 ошибок.
- Новые тесты (`*Protocol*`, `*SharedSerializationTests*`): 576/576 до и после правок shared.
- Полный прогон: 1805 тестов, 1803 прошли, 2 упали, пропущенных 0. Упали `Tests.Meta.UserDeckTests.Update_AllDecksAndSelectedIndex_Persisted` и `GetSelected_AfterChangingSelectedIndex_ReturnsDifferentDeck`: Orleans `Type Dictionary<int, IReadOnlyList<CardType>> is not allowed`. Ошибка была и до N6: на чистом `main` (edbfdba6, отдельный worktree) они падают так же, к N6 отношения не имеют.

## Доработка: padding (после прогона в редакторе)

В редакторе клиентский тест упал на 9 строках. MemoryPack копирует unmanaged-структуры как сырую память вместе с padding: у `KeyValuePair<int, long>` в `Dictionary<enum, long>` 4 байта после ключа, у `CardType?` 3 байта после `HasValue`. В Unity там мусор со стека, на бэкенде нули.

Как теперь сравнивается строка: байты равны, **или** длина та же и обе версии декодируются в одно значение. Значение переводится в текст: `ProtocolValueText` на бэкенде, `ValueText` в клиентском тесте, код одинаковый. В текст идут runtime-тип и все члены, которые пишет MemoryPack, рекурсивно. У простых структур (`Position`, `Vector2`) берутся все поля. DateTime и TimeSpan пишутся в тиках. Golden не менялся, формат прежний.

- Бэкенд `Bytes_MatchGolden` сравнивает так же (`Compare`). Новый тест `PaddingOnlyDifference_IsAccepted_OtherDifference_IsNot`: правка padding проходит, правка значения нет.
- Клиент `AssertWrittenBack`: прочитать golden, записать, при расхождении сравнить длину и значение повторного чтения.
- Проверка строгости: пробный тест (удалён) инвертировал каждый байт каждой из 566 строк. Сравнение пропустило только три вида байтов:
  - padding (3 байта после `HasValue` у `CardType?`, 4 байта после ключа в `Dictionary<enum, long>`);
  - байты `true` у bool (любой ненулевой байт тоже `true`);
  - computed get-only свойства (`Group`, `Target`, `RewardType`): MemoryPack их пишет, но при чтении игнорирует.

  Изменение тега, длины, порядка или значения любого читаемого члена ловится.
- Бэкенд случайно ли стабилен: нет. С грязным `ArrayPool` и стеком, забитым 0xCD, в Debug и в Release (`TieredCompilation=0`) байты совпали с golden. Пять подряд прогонов `*Protocol*` + `SharedSerializationTests` дали 577/577.

## Отличия от A

- Теги не перенумерованы по диапазонам (`6ceb82b0e`): по задаче значения сохраняются, golden не меняется. Комментарий в `INetworkContext` описывает правило "не перенумеровывать, дыры не занимать" без диапазонов.
- `protocol_schema.golden.txt`, `Schema_MatchesGolden` и `ProtocolHash_MatchesSchema` не перенесены, это N7 (`ProtocolVersion`). `ProtocolSchema.BuildText/ComputeHash` уже лежат в тестах для N7.
- `IEventPayload` в B только клиентский, без единой реализации. Раньше для него регистрировался пустой union, теперь у него нет форматтера. Отправка или приём события падают и так и так (тега нет), просто теперь с другой ошибкой. В shared не переносил: событий нет.
- В A вместе с этим переносились события в shared и правки игрового кода A — к B неприменимо.
