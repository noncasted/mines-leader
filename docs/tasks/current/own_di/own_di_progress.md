---
task: own_di
updated: 2026-09-10
---

## Snapshot

| Шаг | Трек | Агент | Статус | Evidence | Блокер |
|-----|------|-------|--------|----------|--------|
| 0 Contract freeze | — | оркестратор | [x] | Abstract заморожен пользователем 2026-09-10 | — |
| 1 Ядро контейнера | A | — | [x] | Runtime/**: ContainerBuilder, слоты, flatten, ReflectionInjector | компиляция: генератор D временно снят с RoslynAnalyzer |
| 2 Тесты | B | — | [x] | 42 EditMode теста, категория Container, красные до A | — |
| 3 Дебагер (UI Toolkit) | C | — | [x] | `Tools/Container Debugger`, empty state | — |
| 3b Граф-вьюер (Graph Toolkit) | F | — | [x] | `Tools/Container Graph`, empty state без исключения | — |
| 4 Генератор инжекторов | D | — | [x] | dll в Plugins/ContainerGenerator, имена в Internal.Generated | — |
| 4b Анализ графа регистраций | D | — | [x] | walker + D2 AssetPostprocessor; CINGR001 warning | — |
| 5 Бенчмарк | E | — | [/] | VContainer измерен; own ждёт A | 1 для своей стороны |
| 6 Миграция и удаление VContainer | — | — | [~] | перенесён в `di_scope_codegen` шаг 4 | — |

## Порядок запуска

```
       ┌─ A ядро ────────────────┐
       ├─ B тесты ───────────────┤
0 ────►├─ C дебагер ─────────────┼───► 6 миграция
       ├─ D генератор → 4b ──────┤
       └─ E бенчмарк ────────────┘
```

Шаг 0 — один агент, остальные ждут. После него A–E идут одновременно.
C и D в шаг 6 не входят: дебагер и генератор не блокируют миграцию.
4b идёт после 4 внутри трека D — это второй заход того же агента, не седьмой.

## Правила треков

- Контракт: `own_di_info.md`. Если код и файл расходятся — побеждает info.
- `Tools/Container/Abstract/**` после шага 0 не правит никто, кроме оркестратора. Нужен новый метод — остановиться и запросить, не дописывать локально.
- Каждый трек пишет только в свой каталог из таблицы владения. Пересечение = конфликт мержа на шести агентах.
- B пишет тесты против заглушек и держит их красными до готовности A. Это нормальное состояние, не блокер.
- E пишет сторону VContainer первой — она не зависит ни от чего, кроме шага 0.
- Выигрыш по перфу не заявляется нигде, пока нет отчёта шага 5.

## Заметки

- Точка входа инъекции — имя метода `Construct`, не атрибут. В проекте уже 46 таких методов против 35 `[Inject]`-полей.
- 12 маркерных интерфейсов `EventLoop.ResolveList<T>` на каждый скоуп — главный источник стоимости сегодня. Порядок элементов в предпосчитанных массивах обязан совпасть с текущим порядком регистрации, иначе тихо поедет порядок `OnSetup` у 36 сервисов.
- `using VContainer.Internal` в `CellAnimator`, `CellVisuals`, `FlagAnimator`, `MatchEventLoop` выглядит мёртвым — проверить и снести в шаге 6.
- Схема «один генерируемый класс на скоуп» вынесена за рамки задачи, см. раздел «Гейт» в info. Шаг 4b делает под неё весь статический анализ, но эмитит по-прежнему план, а не поля.
- Roslyn-генератор не видит `.prefab` и `.unity` — отсюда деление 4b на D1 (Roslyn) и D2 (`AssetPostprocessor`), пишущие `partial` одного класса.
- `builder.Instantiate` — единственная точка инстанцирования префабов в скоуп (`ScopeBuilderExtensions`), метка «зависимость придёт из монобеха» выводится из неё, а не проставляется руками.
- `prefab_catalog` (complete) оказался предусловием 4b: типизированные `GlobalPrefabs.X` дают статический путь от кода до asset GUID.

## Журнал

### [2026-09-10] Шаг 6 перенесён, задача останавливается на 5

Решение пользователя: миграция боевого кода и снос VContainer делаются уже новым
контейнером, в `di_scope_codegen` (шаг 4). Двойная миграция — на слотовый план,
затем на сгенерированный класс — не окупается. `own_di` остаётся `in_progress` и
закрывается вместе с `di_scope_codegen`; из незакрытого здесь остаётся ещё
own-сторона бенчмарка (шаг 5), она снимается там же вместе с третьей колонкой.

### [2026-09-10] Трек F завершён

`Tools/Container Graph` — Graph Toolkit дерево живых контейнеров. Клик: external deps / сервисы / LoadedAssets. Пустой Roots — пустой канвас, не exception. Scratch `.containergraph` в Transient/ gitignored.

### [2026-09-10] Трек D завершён, генератор снова включён

Инжекторы эмитятся в `Internal.Generated`, чтобы не пересечься с VContainer `{Type}GeneratedInjector`. `MarkAssemblyCovered` не пишется, пока nested generics не покрыты. `LoadAssetGroup`/`RequestAssetGroup`/`FindOrLoadScene` — known. CINGR001/002 — warning. Unity console errors = 0.

### [2026-09-10] Трек A завершён

Runtime/**: `ContainerBuilder`, слоты, flatten, eager Singleton/Scoped, `ReflectionInjector`, `ContainerInjectors.Register(Type, Func<int[], IInjector>)`. `Internal.IContainer` собирается. Генератор D временно снят с `RoslynAnalyzer`: `FooGeneratedInjector` совпал с VContainer, CINGR001 ломал компиляцию. Вернём метку после правки имён/severity.

### [2026-09-10] UAC0009 + трек F Graph Toolkit

`DEVELOPMENT_BUILD` заменён на `DEBUG` в `ContainerThread` / `ContainerRegistryDebug` (UAC0009). Abstract расширен для вьюера: `RegistrationInfo.IsExternal`, `LoadedAssetInfo`, `IContainerDiagnostics.LoadedAssets`, `IContainerBuilderScope.AddLoadedAsset`, `ContainerRegistryDebug.RecordLoadedAsset`. `LoadAssetGroup` пишет запись. Graph Toolkit — модуль `Unity.GraphToolkit.Editor` (не пакет). Трек F: `Editor/Tools/ContainerGraph/**`.

### [2026-09-10] Abstract заморожен, фан-аут A–E

Пользователь утвердил Abstract. Перед запуском оркестратор дописал handshake, не меняя публичных сигнатур Abstract:

- `ContainerRegistryDebug.Roots` — пустой живой список; `Changed` — `ViewableDelegate`; `internal AddRoot/RemoveRoot` для трека A.
- `Tools/Container/Runtime/ContainerBuilder.cs` — throw-заглушка, чтобы B/E компилировались. A заменяет реализацией, имя типа `ContainerBuilder` не менять.
- `Internal.Tests.asmdef` ссылается на `VContainer` (нужно E; B VContainer не зовёт).

### [2026-09-10] Снят `IContainer.Parent`

Резолв не ходит в родителя (locked 4, уплощение на Build). Дерево для окна — `IContainerDiagnostics.Parent` / `Children`. Lifetime дочернего и так вложен в родительский. Свойство с `IContainer` убрано.

### [2026-09-10] Namespace Abstract — плоский `Internal`

Пользователь поправил неймспейсы: `Internal.Container` → `Internal`. Locked 1 и сниппеты API в info обновлены. Квалификации `Internal.IViewableDelegate` сняты.

### [2026-09-10] `IContainer.Lifetime : IReadOnlyLifetime`

Поправка контракта до freeze: у `IContainer` свойство `IReadOnlyLifetime Lifetime`. Терминируется в `Dispose`, у дочернего контейнера lifetime вложен в родительский. Сигнатура как у `IBuilder` / `ILoadedScope`.

### [2026-09-10] `event Action` запрещён — `ContainerRegistryDebug.Changed` это `IViewableDelegate`

Поправка контракта до freeze: `public static event Action Changed` → `public static IViewableDelegate Changed { get; }`. В Abstract и в спеке `event Action` больше нет. `Internal.dll` пересобрался, `Changed` — static get-only property типа `IViewableDelegate`, console errors = 0. Locked 15.

### [2026-09-10] Шаг 0 — Abstract/** заморожен как заглушки, фан-аут не запущен

Написан `client/Assets/Common/Internal/Runtime/Tools/Container/Abstract/**`: 12 типов namespace `Internal.Container` один-в-один с разделом API. `Internal.dll` пересобрался, `unity_reflect` видит все 12, console errors = 0, TODO в сигнатурах нет. Тела — только `NotImplementedException` в `ContainerRegistryDebug.Roots`; `ContainerThread.Assert()` реализован под `UNITY_EDITOR || DEVELOPMENT_BUILD`. Конструкторы `RegistrationInfo` / `ResolveRecord` добавлены, иначе readonly-поля нельзя заполнить из Runtime. Фан-аут A–E не запускался: контракт показан на ревью до заморозки.

### [2026-09-10] Задача заведена

Спека написана по итогам разбора VContainer: устройство его source generator'а, стоимость резолва по коду (`Container.cs:152-190`, `IObjectResolverExtensions.cs:41`), IL2CPP-поведение, границы применимости compile-time графа.

Замеров нет. Все утверждения о стоимости выведены из чтения кода — это гипотеза, которую проверяет шаг 5.
