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
| 3 Тесты и бенчмарк | T | 01a089b2-7a4b-73b2-a915-dab7137cb27b | [/] | NUnit locked 2–6 написаны; форма класса красная до эмита G; бенчмарк: колонка Generated пустая | бенчмарк ждёт 1, 1c, 2 |
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
