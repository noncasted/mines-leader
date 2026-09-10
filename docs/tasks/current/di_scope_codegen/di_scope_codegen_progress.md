---
task: di_scope_codegen
updated: 2026-09-10
---

## Snapshot

| Шаг | Трек | Агент | Статус | Evidence | Блокер |
|-----|------|-------|--------|----------|--------|
| 0 Модель рёбер | — | оркестратор | [x] | LoadGlobal+Construct: 16 regs / 10 edges; CardFactory.Create+Build: 14 regs / 16 edges; DelayRunner.updater → Registration#Updater; CINGR003 `dependency`; CINGR004 `CycleA -> CycleB -> CycleC -> CycleA` | — |
| 1 Эмиттер класса скоупа | G | 01a089cf-9e28-7ba0-9f69-507da88d1089 | [x] | класс + `GeneratedScopes.Register("…LoadGlobal+Construct", …)` | — |
| 1b Кросс-сборочный манифест | G | 01a089b2-7a4b-73b2-a915-da9bad5d0fcb | [x] | `[assembly: ContainerInstaller]`; три installer’а без CINGR002 в Verify | — |
| 1c Сущностные скоупы | E | 01a089cf-9e28-7ba0-9f69-50675fbcd696 | [/] | типы разведены; 2+2 класса; 8/4 компонента; CINGR006. Плей-мод не зелёный | плей-мод |
| 2 Стык с рантаймом | R | 01a089b2-7a4b-73b2-a915-daafdc742930 | [/] | `ScopeContainer.Create` — одна точка; `IsGenerated`; дебагер `[generated]`/`[runtime]`. Плей-мод не проверен. Loaders ещё на VContainer | — |
| 3 Тесты и бенчмарк | T | 01a089b2-7a4b-73b2-a915-dab7137cb27b | [/] | Internal.Tests EditMode 40/40; бенчмарк VContainer vs Generated с аллокациями (`own_di/benchmark.md`, 2026-09-10 10:40 UTC) | горячий путь карта/игрок не мерился |
| 4 Миграция, снос VContainer, уборка | — | оркестратор | [ ] | — | ждёт 1, 1b, 1c, 2, 3 |

## Порядок запуска

```
     ┌─ G эмиттер 1 ─┬─ 1b манифест ─┐
0 ───┼─ R рантайм 2 ─┘               ├──► 4 миграция + снос VContainer
     └─ T тесты 3 ───── E скоупы 1c ─┘
```

Шаг 0 закрыт. G, R, T запущены параллельно. Трек E стартует после 1.

## Правила треков

- Контракт: `di_scope_codegen_info.md`. Предшественник: `own_di/own_di_info.md` — читать целиком.
- `Abstract/**` правит только оркестратор. Нет сигнатуры — остановиться и написать оркестратору, не обходить.
- Рантайм-контейнер на слотах не удаляется — он фолбэк.
- Выигрыш по перфу не заявляется, пока нет отчёта шага 3.
- `Scope/EdgeResolver.cs` и `Graph/GraphModel` рёбра — вход шага 1, не переписывать.

## Заметки

- **Сущностные скоупы не покрывались планом — это был недочёт, закрыт шагом 1c.** Три разрыва: якорь `Load(..., view, construct)`, ключ ассета по символу выражения, чтение `ScopeEntityView` из `.unity` со stripped-инстансами.
- Ключ ассета — конкретный тип вьюхи. У карты `CardLocal`/`CardRemote` сегодня один тип `CardScopeEntity` — разводится на два в 1c, первым делом. Поверхность мала: `CardViewFactory` (сделать `Create` дженериком) плюс перегенерация каталога и `CardLocalBindings.g.cs`. У игрока (`LocalPlayerView`/`RemotePlayerView`) так уже сделано.
- **Кросс-сборочные installer'ы есть уже сегодня** — три известных случая (`AddSessionServices`, `AddNetworkConnection`, `AddRemoteEntity`, тела в `Internal`, вызовы из `GamePlay` и `Meta`). Переносить их некуда, это сетевая инфраструктура. Решается шагом 1b: манифест в assembly-атрибутах, `AttributeData` читается из метаданных, в отличие от тел методов и инициализаторов `static readonly`.
- `GraphRegistration` теперь содержит `Dependencies` (`GraphEdge`) и `Ordinal`. Резолвер — `Scope/EdgeResolver`. Типы сводятся только через `TypeNames.ForCode` = `SymbolDisplayFormat.FullyQualifiedFormat`.
- Считать в шаге 4, сколько корней ушло под `[ContainerRuntimeScope]`. Если много — задача не достигла цели.

## Журнал

### [2026-09-10] A+B: дженерик-installer’ы и лишние классы. Таблица из dll

A. Подстановка аргументов-типов метода в месте вызова (локальный обход и манифест). Манифест: `typeof` открытого generic + индексы в blob (`TypeMap` / `ServiceMaps`). Installer помечает возвращённую регистрацию (`ReturnedOrdinal`); `.As<>` / `.WithParameter` на вызове ложатся на неё. Meta не трогали.

B. Класс эмитится только если метод корня в текущей сборке. `IEntityComponent.Register` / `ISceneService.Create` — не корни, вливаются якорем. `GeneratedScopes.Register` на повторный rootId бросает.

Verify ALL PASSED: same-asm generic, manifest generic, `.As` на возврате из другой сборки, Internal→Global класс Internal только в Internal, два инстанса через манифест не оставляют `Box<>`.

Классы **из собранных dll** (`*Container` в IL):

| Сборка | dll | Классы скоупа |
|---|---|---|
| Internal | 13:14, 365568 B | `OptionsContainerRegisterContainer`. Нет `IEntityComponentRegisterContainer` / `ISceneServiceCreateContainer` / `SpriteAnimationRendererRegisterContainer` |
| Global | 13:14, 151040 B | `GlobalScopeExtensionsConstructContainer`. Нет `OptionsContainerRegisterContainer` |
| Meta | 13:25, 89600 B | `MetaScopeExtensionsConstructContainer`. CINGR003 на `IBackendProjection<>` / `IMetaConnectionAwaiter` / `CommandResolver<T>` сняты |
| GamePlay | нет dll | CINGR003 ×67 на боевом Construct/карточных Snapshot; плюс CS0103/CS0266 в эмите `GamePlayerFactoryBuild*Container` (дырки `updater`/`actions`/`round`/`camera`) |
| Menu | нет dll | не собрана (ждёт GamePlay) |
| Flow | нет dll | класс `GameLoopScopeExtensionsConstructContainer` эмитился в прогоне 13:19; в ScriptAssemblies после сбоя GamePlay dll нет |

Плей-мод не гонял: нет всех шести dll. Шаг 4 не начат.

#### CINGR003 после A (только GamePlay; Meta/Menu/Flow в этом прогоне — 0)

67 уникальных, не «нет Register». Группы:

1. **Форма, которую генератор не читает (VContainer, 5):** `LifetimeScope` у `CardFactory`/`GamePlayerFactory`; `IObjectResolver` у `CardActionSyncDispatcher`; `ContainerLocal<IReadOnlyList<IWaitingForPlayers|IMatchStarted|IMatchCompleted>>` у `MatchEventLoop`. Это не own_di-регистрации.
2. **Родитель / сцена, не дырка (остальное):** `ICardConfigs`, `ICardsRegistry`, `IProfile`, `IGameCamera`, `IEntityScopeLoader`, `IGameRandom`, `ICardTargets`, `IPlayerMana`, `IPlayerTurns`, `ICardViewFactory`, `ICardVfxFactory`, `IGameFloatingText`, `IGameResults`, UI bindings (`RoundOverlayUIBindings`, `PlayersOverlayUIBindings`, `GamePause*Bindings`). Либо родительский экспорт не доходит до ребра, либо `ISceneService.Create` не влился в граф корня.
3. **Вложенный `ICardAction.Snapshot`:** пачка `random`/`configs`/`camera`/`vfxFactory` на `ICardAction.cs:39` — вложенный тип из switch/фабрики, не отдельная недостающая `Register` в MetaScope.

CINGR002 ×4 — harvest-копия без usings: `RegisterCommand<RematchCommands.*>` / `SnapshotReceiver` / `AgentObservationHandler` в `ContainerInstallerHarvest*`. Сам `RegisterCommand` из Internal в Meta уже закрыт.

### [2026-09-10] П.0: молчаливый фолбэк закрыт. Классы из dll, не из плей-мода

CINGR001 и CINGR002 — `Error` (locked 5, 11). `ScopeContainer.Create`: нет сгенерированного класса — `InvalidOperationException` с rootId; `builder.Build()` только если `GeneratedScopes.IsRuntimeAllowed` (`[ContainerRuntimeScope]`). Тихого перехода на рантайм-план больше нет.

Полная пересборка. Сгенерированные классы **из собранных dll** (`RegisterGenerated` в IL):

| Сборка | dll | Классы скоупа |
|---|---|---|
| Internal | 12:32, 364544 B, `RegisterGenerated`×4 | `OptionsContainerRegisterContainer`; плюс harvest-корни `IEntityComponentRegisterContainer`, `ISceneServiceCreateContainer`, `SpriteAnimationRendererRegisterContainer` |
| Global | 12:39, 171520 B, `RegisterGenerated`×5 | `GlobalScopeExtensionsConstructContainer` |
| Meta | нет dll | CINGR003: `IBackendProjection<…>`, `IMetaConnectionAwaiter` (RegisterCommand / RegisterBackendProjection не дают сервис в графе); `CommandResolver<T>` |
| GamePlay | нет dll | не собрана в этом прогоне (после красной Meta пайплайн остановился; в Bee лежит stale post-processed 12:18) |
| Menu | нет dll | нет |
| Flow | нет dll | нет |

Плей-мод не гонял: без Menu/GamePlay.dll это не проверка покрытия. Шаг 4 не начат.

### [2026-09-10] П.1–3: почему не было манифеста. Что откатили в резолвере — отдельно

1. `EmitAttributeClass` удалён. `ContainerInstallerAttribute` — обычный тип в Internal. Дубликат в Flow.dll давал `GetTypeByMetadataName` = null, но **это не главная причина пустого манифеста**.
2. Реальная причина красного Menu / пустого Global: `CINGR001 Warning` + `catch` в `ContainerGraphGenerator`. Unity log: 91× `generator exception: ArgumentException Inconsistent language versions (Parameter 'syntaxTrees')` на Internal/Global — harvest `ParseText` без parse options сборки, весь вывод сборки пропускался, сборка зелёная. Второй удар: `NotSupportedException` `TypeSymbol.WithNullableAnnotation` в `TypeNames.ForCode` на `TypeIndex.EnsureIndexed` (CS8785, генератор молча отваливался).
3. Полная пересборка после фикса parse options + TypeNames: Internal и Global **впервые** эмитят классы. Выводы по dll до 11:45/12:11 недействительны.

Родитель: строковая форма снята; `MatchParent` — только точный id; нет манифеста родителя — одна CINGR007. `IViewInjector`/`IEventLoop` — `LoaderServices`, не дырки. Verify: `CSharpGeneratorDriver` Internal→Global→Menu ALL PASSED.

### [2026-09-10] Откат IsExternalHole. Родитель объявлен статически

`IsExternalHole` (интерфейс / чужая сборка / `symbol == null` → дырка) откатан: это меняло locked 4, а не применяло locked 7. Почти все зависимости — интерфейсы, CINGR003 схлопывался; рантайм падал в `request.Get<T>()`.

Форма родителя: `[ContainerScopeParent(typeof(T), nameof(T.LoadX))]` на корне. Резолв ребра: своя регистрация → экспорт родителя по цепочке вверх (ближайший побеждает) → иначе CINGR003. Найденное у родителя — `GraphEdge.Kind=Hole` + `ParentRootId`. `symbol == null` на implementation — CINGR003, никогда дырка. `HoleParam`/`Argument` больше не эмитят `default(T)` — исключение генератора.

Манифест 1b: blob `parent:<id>`. Internal — `[ContainerGraphRoot]` на `InternalScopeLoader.Load+Register` (VContainer `IContainerBuilder` принят как builder-like по имени).

Verify: снятый интерфейс; родитель экспортирует — не ошибка; нет ни у себя ни у родителя — CINGR003; родитель в другой сборке через манифест. ALL PASSED.

Плей-мод после этого — отдельно, шаг 4 не начат.

### [2026-09-10] 1c сдан кодом, плей-мод не зелёный. CINGR003 на родителе

E: `CardLocalScopeEntity` / `CardRemoteScopeEntity` на префабах, `Create<T>`, точка выбора рядом с префабом. Verify 1c ALL PASSED: два класса карты (8 и 4 компонента), два класса игрока со stripped Board, CINGR006 оба пути + «subtype», база не попадает.

Плей-мод до codegen E не гонял. После codegen — не зелёный: Menu красный от CINGR003 на дырах родителя (EdgeResolver на боевых корнях), плюс domain не подхватил новый GamePlay.dll (Missing Script на слоте вьюхи).

Оркестратор поправил EdgeResolver: неразрешённый **интерфейс** или тип **из чужой сборки** — дырка (родитель), не CINGR003. Снятый **локальный класс** той же сборки по-прежнему CINGR003 (`UnregisteredDependency`). Verify ALL PASSED. Это не молчаливый фолбэк генератора: класс всё ещё эмитится целиком, недостающее из родителя — параметр конструктора (locked 7).

Шаг 4 не начинаю: нет зелёного плей-мода.

### [2026-09-10] G дописал `GeneratedScopes.Register`

В том же generated-файле: `RegisterGenerated()` + `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`.
rootId LoadGlobal: `Global.Setup.GlobalScopeExtensions.LoadGlobal+Construct`.
`CreateChild` → `ScopeContainer.CreateChild(this)`. Verify ALL PASSED.

### [2026-09-10] Трек G: шаг 1 и 1b закрыты. Стартую E

Verify ALL PASSED, включая эмит LoadGlobal и манифест.

- Класс: `Global.Setup.GlobalScopeExtensionsLoadGlobalContainer`. Ноль `IResolvePlan`. Transient = метод. `[ContainerRuntimeScope]` глушит точечно. CINGR003 не эмитит класс.
- Маркеры: порядок Bronze→Gold = регистрация.
- 1b: `GraphEmitter` пишет `[assembly: ContainerInstaller(...)]`. AddSessionServices / AddNetworkConnection / AddRemoteEntity в харнессе без CINGR002. Harvest local functions сохраняет порядок.

Дыры, не закрывающие 1:
- Эмит не вызывает `GeneratedScopes.Register` — стык R без этого остаётся на рантайм-плане. Дописываю G.
- `CreateChild()` в эмите — `NotSupportedException`; у R есть `ScopeContainer.CreateChild`.
- Атрибут манифеста эмитился «если типа нет». Оркестратор положил стабильный `Generated/ContainerInstallerAttribute.cs`.
- `IProvides<T>` оркестратор уже добавил в Abstract.

Трек E стартует. Первый пункт — развести `CardLocalScopeEntity` / `CardRemoteScopeEntity` и плей-мод, до всякого codegen.

### [2026-09-10] Трек R: точка выбора есть, плей-мод не проверен

`ScopeContainer.Create(rootId, …)`: если `GeneratedScopes.IsRegistered` — фабрика, иначе `builder.Build()`. Исключение фабрики на план не откатывается. `ContainerBuilder.Build()` для тестов всегда план.

`GeneratedContainer.Resolve` читает словарь. `IsGenerated = true` в конструкторе хелпера. Дебагер: бейдж `[generated]`/`[runtime]`, класс `.tree-item--generated`. Пустое окно без play mode не трогал.

Плей-мод не гонял: MCP-сессии нет; `Internal.dll` в момент отчёта красная из‑за G (CS0102 на вложенном `Diagnostics` — G уже переименовал в `GeneratedDiagnostics`).

Боевые loaders (`ServiceScopeLoader` / `EntityScopeLoader`) на VContainer — шаг 4. Точка выбора пока ниоткуда в проде не зовётся.

R не добавлял `IProvides<T>` (правильно). Оркестратор добавил в Abstract. `IBuilder.Provide` не добавлялся.

Внутренний `IContainerTree` + `is GeneratedContainer` / рефлексия `_exports` — обход, чтобы стыковать дерево и уплощение ребёнка без правки Abstract. Не идеал; G может наследовать `GeneratedContainer` и не упираться в рефлексию.

### [2026-09-10] Трек T: тесты locked 2–6, бенчмарк без третьей цифры

NUnit `ScopeCodegenDiagnosticTests` / `ScopeCodegenEmitTests` / `ScopeCodegenFixtures`, категория Container. Форма класса (`ScopeCodegenRootsLoadGeneratedContainer`) специально красная до эмита G — не ослаблял.

Зелёные сейчас: контракт CINGR003/004, `ContainerRuntimeScopeAttribute` на методе, `RuntimePlan.IsGenerated == false`, `GeneratedHost_IsThirdColumn`.

Красный и корректный: `CINGR001_UncoveredSyntax_IsError` — в `GraphDescriptors` до сих пор `Warning`, спека locked 5 говорит Error. Оркестратор не переключает severity, пока G/R в полёте: Error на непокрытом синтаксисе боевых installer'ов снова покрасит Unity (так уже было в own_di). G в `ScopePlan` уже не эмитит при CINGR001 независимо от severity.

Бенчмарк: `GeneratedContainerBenchmarkHost.Open()` бросает, цифр нет. Таблица `own_di/benchmark.md` — три колонки, Generated = —. `GC.GetAllocatedBytesForCurrentThread` на этом Editor всегда 0. First ResolveAll item count: VContainer 16 vs Own 26. Горячий путь карта/игрок не мерился.

Verify шага 0 не сломан (ALL PASSED).

### [2026-09-10] Шаг 0 закрыт. Abstract: `IContainerDiagnostics.IsGenerated`

`GraphRegistration.Dependencies` + `Scope/EdgeResolver`: для каждой регистрации параметры `TypeAnalyzer` (ctor + `Construct`) резолвятся в другую регистрацию, коллекцию, implicit (`IContainer` / `IReadOnlyLifetime` / `ILifetime`) или дырку (`ParameterHole` / `WithParameter`). Не нашлось — `CINGR003 Error` с именем параметра. Топосорт + `CINGR004 Error` с путём.

Проверка (`dotnet run` `client/Tools~/ContainerGenerator.Verify`, net9.0):

- `Global.Setup.GlobalScopeExtensions.LoadGlobal+Construct`: 16 регистраций, 10 рёбер. `DelayRunner.updater` → Registration#0 (`Updater`, PrefabInstance). `CameraUtils.camera` → `CurrentCamera`. `UIStateMachine.lifetime` → Implicit. `LoadingScreen.updater` → Construct-ребро на тот же `Updater`. `ItchLanguageProvider.api` → last-wins `ItchLanguageExternAPI`. Ноль CINGR003/004.
- `GamePlay.Cards.CardFactory.Create+Build`: 14 регистраций, 16 рёбер. Flatten сохранил `RegisterInstance(cardId)` перед `AddCardLocalComponents`. `CardDropArea` в списке раньше `CardContext` (как в реальном installer'е), но `ConstructionOrder` ставит `CardContext` первым — locked 16. `HandEntryHandle.card` → `LocalCard`. Ноль CINGR003/004.
- Снятый `UnregisteredDependency`: `CINGR003` «Cannot resolve parameter 'dependency' of type 'global::Sample.UnregisteredDependency'».
- Цикл A→B→C→A: `CINGR004` «Circular dependency: global::Sample.CycleA -> global::Sample.CycleB -> global::Sample.CycleC -> global::Sample.CycleA».
- `IReadOnlyList<IAchievementTier>`: Kind=Collection, порядок Bronze→Gold = порядок регистрации.

Реальные боевые `LoadGlobal` / `CardFactory.Build` в Unity-компиляции резолвером шага 0 не гонялись: без родителя и без 1c/1b там будут CINGR003 на `BackendOptions`, `IGameContext`, компоненты префаба. Генератор пока EdgeResolver не вызывает — Unity не краснеет. G подключает резолвер на эмите.

`Register<T>()` в walker теперь пишет implementation в `ServiceTypes` (как runtime `AsSelf()`).

Abstract, добавление: `IContainerDiagnostics.IsGenerated { get; }`. Runtime-план оставляет `false`. Нужно шагу 2, чтобы дебагер отличил сгенерированный скоуп. Обе спеки обновлены.

`[ContainerRuntimeScope]` заведён: `Generated/ContainerRuntimeScopeAttribute.cs`, цель — метод (как `ContainerGraphRoot`). `ReferenceSymbols.RuntimeScopeAttribute`. G не изобретает свой атрибут.

### [2026-09-10] Решения пользователя: миграция переезжает сюда, добавлен шаг 1c

1. `own_di` останавливается на шаге 5. Шаг 6 (перевод боевого кода, снос VContainer)
   перенесён в шаг 4 этой задачи: мигрировать дважды — на слотовый план, потом на
   сгенерированный класс — незачем. Блокер «`own_di` не закрыт» снят.
2. Отсутствие шага под инстанцируемые скоупы признано недочётом плана. Заведён шаг
   1c, трек E, пятый агент. Locked дополнен 13-16: якорь, ключ ассета, варианты,
   источник порядка конструирования.

Проверено по коду перед правкой контракта: `IEntityScopeLoader.Load` имеет две
перегрузки и в обеих `view` и `construct` стоят в одном вызове — этого достаточно
как якоря, атрибут для привязки префаба не нужен. `view.CreateViews(builder)`
вызывается внутри `Load` между `construct` и построением контейнера.
`GamePlayerFactory` берёт вьюху из сериализованных полей `LocalPlayerView` /
`RemotePlayerView`, `CardFactory` — из `GamePlayPrefabs.CardLocal` / `.CardRemote`,
и оба имеют один тип `CardScopeEntity`.

Правка от пользователя по итогам: не трассировать выражение `view`, а развести типы
вьюх у карты — как у игрока. Ключ ассета стал типом (locked 14), генератор проще,
вариант скоупа виден в типе. Разведение типов — первый пункт 1c.

Усилено до инварианта: один конкретный тип вьюхи — ровно один ассет, биекция,
дубликат = `CINGR006` по всему проекту. Убирает из генератора разрешение
неоднозначностей целиком. Абстрактные базы под правило не попадают.

### [2026-09-10] Фаза 0: CardScope / GamePlayerScope не покрываются. Шаг 0 не начат.

`own_di` не закрыт: `status: in_progress`, шаг 6 pending, `CardScope`/`GamePlayerScope` всё ещё `: LifetimeScope`.

Покрытие сущностных скоупов — нет. Корни `CardFactory.Build` и `GamePlayerFactory.Build` walker видит (local function `Build` + `IEntityBuilder`). Префабы карт есть в `ContainerGraph.Assets.Prefabs`. Но:

- `CreateViews` зовётся из `EntityScopeLoader`, вне корня. D2 не склеивает ассеты с `Build`.
- `GamePlayer` живёт в сцене: D2 из `.unity` берёт только `SceneServicesFactory`. `_autoDetected` у `LocalPlayerView`/`RemotePlayerView` в `Game_Field.unity` (Board, PlayerActiveStatusView, HandView) в манифесте нет.
- Один метод — два графа (`isLocal` / `Owner.IsLocal`), не Alternative одного сервисного типа. Спека эмитит один `{Root}{Method}Container`.

Выигрыш генерации на этом пайплайне — холодный старт. Горячий путь (карта / игрок) остаётся на рантайм-плане. Шаг 0 не стартовал. **Закрыто шагом 1c** — см. запись выше.

### [2026-09-10] Кросс-сборочность — не «стеречь», а решать

Первичная проверка ограничения была сделана по двум корням (`Global`, карточный) и
обобщена неверно. Реальных нарушений минимум три, все — вызовы installer'ов из
`Internal` в `GamePlay` / `Meta`. Locked 11 переписан, добавлен шаг 1b.

Полезное следствие: `GraphEmitter` уже эмитит нужные для манифеста данные, но в
форме `static readonly` массивов — их инициализатор живёт в статическом
конструкторе и через границу сборки не читается. Смена цели эмита на
assembly-атрибуты делает существующую работу кросс-сборочной.

### [2026-09-10] Задача заведена

Повод: сгенерированный `AchievementRowGeneratedInjector` резолвит каждое ребро
через `plan.Get<T>(_slot)` — структурно то же, что `resolver.ResolveOrParameter`
в VContainer, только дешевле внутри. Дерево по-прежнему собирается в рантайме.

Это не ошибка агента: шаг 4 в `own_di` буквально требует `plan.Get<T>(slot)`, а
схема с полями вынесена там в раздел «Гейт». Гейт снимается этой задачей.

Предусловие гейта оказалось выполнено раньше срока: `GraphWalker` (~1125 строк)
уже разбирает все семь случаев (`ContainerGraphOrigin`), включая `SwitchFactory`,
`Alternative` и дырки.

2026-09-10. Метод инжекта ищется по `[Internal.Inject]`, а не по имени `Construct`: `TypeAnalyzer` берёт помеченный метод (любое имя, ровно один на тип), `EdgeResolver` кладёт имя в `GraphEdge.Method`, `ScopeEmitter` зовёт его по этому имени. `Construct` без атрибута генератор не видит. Тест: Verify "[Inject] picks method by attribute, not name".

2026-09-10. Аллокации в бенчмарке были 0 из-за меры: `GC.GetAllocatedBytesForCurrentThread` в Unity Mono — заглушка. Замер переведён на `ProfilerRecorder` `GC Allocated In Frame` (байт в байт, все потоки), 11 прогонов: время — медиана, байты — минимум. Разбивка Build по шагам (`GeneratedContainer_BuildAllocations_ByStep`) показала 62% на installer: каждая `ServiceRegistration` сразу создавала `List<Type>` и пустой `Dictionary<Type, object>`, которые читает только `TryGetHole`. Обе коллекции ленивые — Build 17443 → 11315 байт (VContainer 32024), Build + резолв всех 11499 против 44732. Цифры и остаток по шагам — `own_di/benchmark.md`.

2026-09-10. `IRegistration` и обёртка `ContainerRegistration` удалены: цепочка installer'а (`Register`, `As`, `WithParameter`, `AsSelfResolvable`, `WithScopeLifetime`, `AsSessionCallback`) работает прямо с `IServiceRegistration`, у которого есть `IBuilder Builder`. Методы интерфейса переименованы в `AddServiceType`/`SetParameter`: одноимённые `As`/`AsSelf`/`WithParameter` перехватывали вызовы расширений, и `GraphWalker` (узнаёт примитивы по классу `BuilderExtensions`) их бы не увидел. Билдер попадает в `ContainerBuilder` через `AttachBuilder` из конструкторов `RootBuilder`/`ScopeBuilder`/`EntityBuilder`; `GraphWalker.IsInstaller` не считает installer'ом методы типов `IContainerRegistry` (иначе `AttachBuilder(IBuilder)` попадал в манифест). `IContainerRegistry.AddSelfResolvable` снят — флаг никто не читал. Проверка: сгенерированные классы всех 9 сборок совпадают с эталоном до правки, id манифеста тоже; Verify ALL PASSED (+ тест на `AttachBuilder`); Internal.Tests 40/40. Build −816 байт (−24 на регистрацию). Плей-мод не проверен.
