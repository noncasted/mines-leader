## Scope codegen — Результат

### Статус: 0, 1, 1b закрыты; 1c кодом есть, плей-мод нет; шаг 4 не начат

### Что сделано

- Фаза 0: разведка показала, что `CardScope` / `GamePlayerScope` планом не покрывались.
- Контракт доработан по решениям пользователя: миграция и снос VContainer перенесены из `own_di` в шаг 4; заведён шаг 1c (трек E) под сущностные скоупы; locked дополнен пунктами 13-16.
- Шаг 0: `GraphRegistration` получил рёбра, `Scope/EdgeResolver` соединяет `GraphDocument` и `TypeAnalyzer`, топосорт, `CINGR003`/`CINGR004`.
- Abstract: `IContainerDiagnostics.IsGenerated`.
- Шаг 3 (T): NUnit на locked 2–6; бенчмарк с пустой колонкой Generated, без заявления выигрыша.
- Шаг 2 (R): `ScopeContainer.Create` + `GeneratedScopes`; дебагер отличает; плей-мод не проверен.
- Abstract: `IProvides<T>`, `ContainerInstallerAttribute`.
- Шаг 1 (G): `GlobalScopeExtensionsLoadGlobalContainer`, ноль IResolvePlan.
- Шаг 1b (G): assembly-манифест, три кросс-сборочных installer’а в Verify.

### Измененные файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|
| `client/Tools~/ContainerGenerator/Graph/GraphModel.cs` | `GraphEdge`, `Dependencies`, `Ordinal`, `ScopeGraph` | 0 | тип ребра + clone |
| `client/Tools~/ContainerGenerator/Graph/GraphDescriptors.cs` | `CINGR003` Error, `CINGR004` Error | 0 | descriptors |
| `client/Tools~/ContainerGenerator/Graph/GraphWalker.cs` | ordinals, `AddCall`, `Register<T>` → AsSelf | 0 | flatten source order |
| `client/Tools~/ContainerGenerator/Scope/EdgeResolver.cs` | резолв рёбер + топосорт | 0 | Verify ALL PASSED |
| `client/Tools~/ContainerGenerator/Scope/TypeIndex.cs` | lookup по FullyQualifiedFormat | 0 | DelayRunner.updater совпал |
| `client/Tools~/ContainerGenerator.Verify/**` | харнесс шага 0 | 0 | 5 PASS |
| `Tools/Container/Abstract/IContainerDiagnostics.cs` | `bool IsGenerated { get; }` | 0 | добавление |
| `Tools/Container/Runtime/ContainerDiagnostics.cs` | default `false` | 0 | компилируется |
| `Tools/Container/Generated/ContainerRuntimeScopeAttribute.cs` | `[ContainerRuntimeScope]` на методе-корне | 0 | AttributeTargets.Method |
| `Internal/Tests/Editor/Container/ScopeCodegen*.cs` | locked 2–6, форма класса, RuntimeScope | 3 | NUnit, часть красная до G |
| `Internal/Tests/Editor/Container/Benchmarks/GeneratedContainerBenchmarkHost.cs` | третья сторона, `Open()` бросает | 3 | не выдумывает цифры |
| `docs/tasks/current/own_di/benchmark.md` | колонка Generated = — | 3 | VContainer и план сняты |
| `Runtime/GeneratedScopes.cs`, `GeneratedContainer.cs` | реестр фабрик + хелпер IContainer | 2 | Resolve = словарь |
| `Runtime/Scopes/Services/ScopeContainer.cs` | одна точка выбора | 2 | не зовётся из loaders |
| `Editor/Tools/Container/ContainerDebuggerWindow.*` | бейдж generated/runtime | 2 | минимально |
| `Abstract/IProvides.cs` | `IProvides<T>` | 0/2 | добавление оркестратора |
| `Generated/ContainerInstallerAttribute.cs` | стабильный манифест 1b | 1b | генератор не дублирует |
| `Tools~/ContainerGenerator/Scope/ScopeEmitter.cs` | эмит класса скоупа | 1 | Verify LoadGlobal |
| `Tools~/ContainerGenerator/Graph/GraphEmitter.cs` | assembly-атрибуты | 1b | не static readonly |

### Проверки

| Проверка | Шаг | Результат |
|----------|-----|-----------|
| Граф с рёбрами строится для `LoadGlobal` и `CardFactory.Build` | 0 | PASS. Фикстуры тех же корней: LoadGlobal+Construct 16/10, CardFactory.Create+Build 14/16. `dotnet run` Tools~/ContainerGenerator.Verify |
| Снятая регистрация → `CINGR003` с именем параметра | 0 | PASS. `parameter 'dependency' of type 'global::Sample.UnregisteredDependency'` |
| Цикл → `CINGR004` с полным путём | 0 | PASS. `global::Sample.CycleA -> global::Sample.CycleB -> global::Sample.CycleC -> global::Sample.CycleA` |
| Класс скоупа для `LoadGlobal` компилируется | 1 | PASS Verify: `GlobalScopeExtensionsLoadGlobalContainer` |
| Ноль обращений к `IResolvePlan` в сгенерированном классе | 1 | PASS Verify |
| Порядок маркерных массивов = порядок регистрации | 1 | PASS Verify Bronze→Gold |
| `Transient` не имеет поля, только метод | 1 | PASS Verify `CreatePopup()` |
| `[ContainerRuntimeScope]` отключает генерацию точечно | 1 | PASS Verify |
| `Resolve` на сгенерированном скоупе не создаёт объектов | 2 | код `GeneratedContainer.ReadExport` — только словарь. Тестом T не покрыто |
| Дебагер отличает сгенерированный скоуп от рантайм-ного | 2 | `[generated]` / `[runtime]` в дереве |
| Плей-мод: меню + матч против бота | 2 | не проверен |
| `CardFactory.Build`: два класса, 8 и 4 компонента, ноль `IResolvePlan` | 1c | PASS Verify: Local 8 / Remote 4, ноль IResolvePlan. Плей-мод не проверен |
| `GamePlayerFactory.Build`: компоненты из `Game_Field.unity`, включая stripped `Board_*` | 1c | PASS Verify stripped Board. Плей-мод не проверен |
| Дубликат типа вьюхи на двух ассетах → `CINGR006` с обоими путями | 1c | PASS Verify: оба пути + «subtype» |
| Инвариант тип↔ассет проверен на всём проекте, нарушений нет | 1c | сканер D2 + CINGR006; живой прогон по проекту в редакторе не делался |
| Ноль вхождений `VContainer` в `client/Assets` | 4 | — |
| Бенчмарк: третья колонка рядом с VContainer и планом | 3 | каркас есть, цифр Generated нет (ждёт 1, 1c, 2) |
| Бенчмарк горячего пути: создание карты и игрока | 3 | ждёт 1c |
| Корней под `[ContainerRuntimeScope]`: сколько | 4 | — |

### Бенчмарк

Три стороны на одном графе: VContainer / рантайм-план / сгенерированный класс.
Заполняется шагом 3, пересдаётся после шага 4.

### Не сделано / отложено

- EdgeResolver не подключён к `ContainerGraphGenerator`: на боевых корнях без родителя/1b/1c он дал бы CINGR003 (`BackendOptions`, `IGameContext`, компоненты префаба). Подключает трек G на эмите.
- Реальные Unity-компиляции `Global` / `GamePlay` шагом 0 не гонялись — только структурные фикстуры тех же корней.
- Плей-мод 1c и 2 не зелёный. Шаг 4 (миграция, снос VContainer) не начат.
