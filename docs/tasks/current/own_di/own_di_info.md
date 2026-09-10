---
task: own_di
status: in_progress
migration_moved_to: di_scope_codegen
phase: implementation
created: 2026-09-10
updated: 2026-09-10
total_steps: 9
completed_steps: [0]
blocked_steps: []
agents: 7
abstract_frozen: true
---

# Own DI Container — spec

Authoritative contract. If this file and the code disagree, this file wins. Do not invent API that is not written here.

Repo root: `/projects/mines-leader`
Unity project: `/projects/mines-leader/client`
Container root: `client/Assets/Common/Internal/Runtime/Tools/Container`

---

## Что я хочу

VContainer уходит. На его месте — свой контейнер, в котором резолв это индексация массива, а не хэш-лукап с проходом по цепочке скоупов.

Сегодня один параметр конструктора стоит: линейный скан `parameters`, хэш-лукап `(Type, object)` на каждом уровне цепочки скоупов, для синглтона из дочернего скоупа — второй лукап `registry.Exists` и рекурсия в родителя, затем `ConcurrentDictionary.GetOrAdd` + `Lazy<object>`, затем `castclass`. Это на **каждое ребро графа**.

Должно стать: чтение поля или индексация массива по заранее посчитанному слоту.

Регистрация остаётся ручной и явной — `builder.Register<T>()` в коде, видимый по usings. Генератор не заменяет регистрацию, он генерирует **проводку**: вызовы `Construct(...)` и план резолва. Это ровно та граница, которая уже принята в проекте: кодоген вместо ассетов — да, кодоген вместо структуры кода — нет.

---

## Цель

1. Свой контейнер в `Tools/Container`: реестр, план резолва, дерево скоупов, диспоз.
2. Кодоген инжекторов вместо рефлексии: `Construct(...)` вызывается напрямую.
3. Тесты на семантику контейнера (edit-mode, NUnit).
4. UI Toolkit окно: смотреть собранный граф в рантайме.
5. Бенчмарк: новый контейнер против VContainer на одном графе, цифры в отчёт.
6. Миграция `Internal`-скоупов с VContainer, удаление пакета.

---

## Контекст

### Что уже есть и остаётся

Проект **не** зовёт VContainer напрямую — поверх него лежит своя абстракция в `Common/Internal/Runtime/Scopes`:

- `IBuilder` (`Services` / `Events` / `Lifetime`), `IScopeBuilder`, `IEntityBuilder`.
- `BuilderExtensions`: `Register<T>`, `Register<TInterface,TImpl>`, `RegisterInstance<T>`, `RegisterComponent<T>`, `As<T>`, `WithParameter`, `AsSelf`, `AsSelfResolvable`, `Inject<T>`, `WithScopeLifetime`.
- `IEventLoop` с фазами: `BeforeBuild` → `Construct` (`IScopeBaseSetup` → `IScopeBaseSetupAsync` → `IScopeSetup` → `IScopeSetupAsync` → `IScopeSetupCompletion` → `IScopeSetupCompletionAsync`) → `Loaded` → `Dispose`.
- `ILifetime` / `IReadOnlyLifetime` — своё, VContainer к нему отношения не имеет.
- `IEntityComponent.Register(IEntityBuilder)` — компонент сам регистрирует себя, вызов идёт из `ScopeEntityView.CreateViews`.

**Эта поверхность API не меняется.** Игровой код (287 `.Register*` call sites) не переписывается.

### Где именно течёт VContainer

62 файла с `using VContainer`, но типы VContainer вне `Common/Internal` встречаются в 35 строках:

| Место | Что течёт |
|---|---|
| `IServiceCollection` / `IRegistration` | `RegistrationBuilder`, `IContainerBuilder` |
| `InstanceInjection`, `ServiceCollection.Resolve` | `IObjectResolver` |
| `ViewInjector`, `IScopeEntityView`, `InternalLoadedScope` | `LifetimeScope` |
| `BuilderExtensions` | `VContainer.Lifetime` в дефолтных значениях |
| `CardScope`, `GamePlayerScope` | `: LifetimeScope` |
| `CardFactory`, `GamePlayerFactory` | `LifetimeScope parentScope` в конструкторе |
| `CardActionSyncDispatcher` | `IObjectResolver resolver` в конструкторе |
| `EventLoop.ResolveList<T>` | `ContainerLocal<IReadOnlyList<T>>` |

Часть `using VContainer.Internal` (`CellAnimator`, `CellVisuals`, `FlagAnimator`, `MatchEventLoop`) — мёртвые, проверить и снести.

### Горячая точка

`EventLoop.ResolveList<T>()` резолвит `ContainerLocal<IReadOnlyList<T>>` для **12 маркерных интерфейсов на каждый скоуп**. Это самый дорогой путь в VContainer (`CollectionInstanceProvider`) и он же конструирует все сервисы этапа — в коде уже висит комментарий об этом. В новом контейнере эти 12 списков — предпосчитанные массивы, отсортированные на этапе Build.

Реализаторов: `IScopeSetup` 36, `ISceneService` 15, `IEntityComponent` 12, `IScopeBaseSetup` 3, `IScopeLoaded` 2, `IScopeSetupCompletion` 0, `IScopeDispose` 0.

### Форма инъекции

46 методов `void Construct(...)` против 35 `[Inject]`-полей. Method injection уже доминирует. `[Inject]` уходит вместе с VContainer — точка входа определяется **именем метода `Construct`**, атрибут не нужен.

### Масштаб графа

18 `Lifetime.Singleton`, 9 `Scoped`, 5 `Transient` в явных вызовах. 2 подкласса `LifetimeScope` (`CardScope`, `GamePlayerScope`) — сущностные скоупы, создаются на карту и на игрока, это горячий путь. 40 префабов, 7 сцен на весь проект.

---

## Locked decisions

1. Namespace — `Internal` (плоский, как `Tools/Profiling`).
2. **Main-thread only.** Никаких `ConcurrentDictionary`, `Lazy<T>`, `volatile`. Это осознанный отказ от потокобезопасности VContainer и часть выигрыша. Нарушение ловится `ContainerThread.Assert()` в DEBUG.
3. **Резолв идёт по int-слоту, не по `Type`.** `Type` → слот разрешается один раз на `Build()`, дальше только индексация.
4. **Цепочка скоупов уплощается на Build дочернего скоупа.** Карта `Type → слот` родителя копируется в дочернюю; подъёма по родителям в рантайме нет.
5. **Eager-конструирование в топологическом порядке.** Порядок считается на Build. Ленивость — только явная, через `Lazy<T>`-регистрацию, не по умолчанию.
6. **Цикл в графе — ошибка на Build**, с полным путём цикла в тексте. Не `StackOverflow`.
7. **Отсутствующая зависимость — ошибка на Build**, а не на первом резолве. Build валидирует весь граф целиком.
8. Точка инъекции — метод с именем `Construct`. Один на тип. Атрибутов нет. Поля и свойства не инжектятся вообще: `[Inject]`-поля мигрируют в `Construct`.
9. **Кодоген инжекторов — оптимизация, не требование.** Рантайм обязан работать на рефлексивном фолбэке. Генератор ускоряет; его отсутствие не ломает сборку. Это развязывает трек D от всех остальных.
10. **Молчаливого фолбэка нет.** Если генератор пометил сборку как покрытую, а инжектора для типа нет — исключение с именем типа. Тихий откат на рефлексию делает регрессию невидимой.
11. Публичная поверхность `IBuilder` / `BuilderExtensions` / `IEventLoop` / `ILifetime` не меняется. Меняются только их внутренности.
12. `VContainer.Lifetime` → `Internal.ServiceLifetime` с теми же тремя значениями и тем же дефолтом (`Singleton`).
13. Диагностика — **отдельный read-only интерфейс** `IContainerDiagnostics`, снимается с живого контейнера, не требует включённого дебаг-режима для сбора структуры (только для истории резолвов).
14. Пакет `Assets/Plugins/VContainer` удаляется в последнем шаге, не раньше.
15. Никакого `event Action` / `event Action<T>`. Уведомления — `IViewableDelegate` (Advise + lifetime), как везде в Internal.

---

## API (frozen contract)

Всё, что ниже, пишется в шаге 0 оркестратором как компилируемые заглушки (`throw new NotImplementedException()`), и с этого момента **сигнатуры не меняются**. Треки A–E работают против них.

### Регистрация

```csharp
namespace Internal {
    public enum ServiceLifetime { Transient, Scoped, Singleton }

    public interface IContainerRegistry {
        IServiceRegistration Add(Type implementation, ServiceLifetime lifetime);
        IServiceRegistration AddInstance(Type serviceType, object instance);
        IServiceRegistration AddComponent(Type serviceType, UnityEngine.Object component, ServiceLifetime lifetime);
        void AddInjection(object target);
        void AddSelfResolvable(IServiceRegistration registration);
    }

    public interface IServiceRegistration {
        Type ImplementationType { get; }
        ServiceLifetime Lifetime { get; }
        IServiceRegistration As(Type serviceType);
        IServiceRegistration AsSelf();
        IServiceRegistration WithParameter(Type type, object value);
    }
}
```

### Резолв

```csharp
namespace Internal {
    public interface IContainer : IDisposable {
        IContainerDiagnostics Diagnostics { get; }
        IReadOnlyLifetime Lifetime { get; }

        object Resolve(Type type);
        T Resolve<T>();
        bool TryResolve(Type type, out object instance);
        IReadOnlyList<T> ResolveAll<T>();

        void Inject(object target);
        void InjectGameObject(UnityEngine.GameObject target);

        IContainerBuilderScope CreateChild();
    }

    public interface IContainerBuilderScope : IContainerRegistry {
        IContainer Build();
        void AddLoadedAsset(string label, string groupName);
    }
}
```

`ResolveAll<T>` возвращает предпосчитанный на Build массив. Пустой список — валидный результат, не исключение.

`IContainer.Lifetime` — жизнь контейнера. Терминируется в `Dispose`. Lifetime дочернего контейнера вложен в родительский.

Родитель на `IContainer` не торчит: резолв не поднимается по цепочке (locked 4). Дерево скоупов — `IContainerDiagnostics.Parent` / `Children`.

### Инжектор (то, что эмитит генератор)

```csharp
namespace Internal {
    public interface IInjector {
        object Create(IResolvePlan plan);
        void Construct(object instance, IResolvePlan plan);
    }

    public interface IResolvePlan {
        object Get(int slot);
        T Get<T>(int slot);
    }
}
```

Сгенерированный класс для типа `Foo` называется `FooGeneratedInjector`, лежит в **той же сборке**, что и `Foo`, реализует `IInjector`. Слоты передаются в конструктор инжектора при Build — инжектор не знает про `Type` вообще.

Регистрация инжекторов — явная, не через `Assembly.GetType`: генератор пишет на сборку один `ContainerInjectors_{Assembly}.Register()` со словарём `Type → Func<IInjector>`, вызываемый из `[RuntimeInitializeOnLoadMethod]`. Никакого рефлексивного поиска по имени, никакого `[Preserve]`-танца со стриппингом.

### Диагностика

```csharp
namespace Internal {
    public interface IProvides<T> {
        void Construct(T target);
    }

    public interface IContainerDiagnostics {
        string Name { get; }
        bool IsGenerated { get; }
        IContainerDiagnostics Parent { get; }
        IReadOnlyList<IContainerDiagnostics> Children { get; }
        IReadOnlyList<RegistrationInfo> Registrations { get; }
        IReadOnlyList<int> BuildOrder { get; }
        IReadOnlyList<LoadedAssetInfo> LoadedAssets { get; }
        bool IsHistoryEnabled { get; set; }
        IReadOnlyList<ResolveRecord> History { get; }
    }

    public readonly struct RegistrationInfo {
        public readonly int Slot;
        public readonly Type ImplementationType;
        public readonly IReadOnlyList<Type> ServiceTypes;
        public readonly ServiceLifetime Lifetime;
        public readonly IReadOnlyList<int> Dependencies;
        public readonly bool IsInstantiated;
        public readonly bool IsGenerated;   // инжектор сгенерирован, не рефлексия
        public readonly bool IsExternal;    // слот скопирован с родителя (зависимость извне)
    }

    public readonly struct LoadedAssetInfo {
        public readonly string Label;
        public readonly string GroupName;
    }

    public readonly struct ResolveRecord {
        public readonly int Slot;
        public readonly double Milliseconds;
        public readonly int Frame;
    }
}
```

`Registrations` / `BuildOrder` / `Dependencies` доступны всегда. `History` пишется только при `IsHistoryEnabled`.

### Реестр контейнеров для окна

```csharp
namespace Internal {
    public static class ContainerRegistryDebug {
        public static IReadOnlyList<IContainerDiagnostics> Roots { get; }
        public static IViewableDelegate Changed { get; }
        public static void RecordLoadedAsset(IReadOnlyLifetime lifetime, string label, string groupName);
    }
}
```

Наполняется только под `UNITY_EDITOR || DEBUG` (не `DEVELOPMENT_BUILD` — UAC0009).

`LoadAssetGroup` пишет `RecordLoadedAsset(builder.Lifetime, label, group.Name)`. Билдер на `Build` забирает это через `TakeLoadedAssets` и кладёт в `IContainerDiagnostics.LoadedAssets`. `AddLoadedAsset` на билдере — прямой путь для тестов.

`Roots` — живой список, пустой список валиден (не throw). `Changed` — живой `ViewableDelegate`. Трек A вызывает `internal static AddRoot` / `RemoveRoot` (та же сборка `Internal`) на `Build` корневого контейнера и на его `Dispose`.

### Точки входа Runtime (не Abstract; трек A реализует, B/E вызывают)

Публичный билдер, чтобы тесты и бенчмарк собирали контейнер без правки Abstract. Имя типа заморожено: `ContainerBuilder`.

```csharp
namespace Internal {
    public sealed class ContainerBuilder : IContainerBuilderScope
    {
        public ContainerBuilder(string name = "Root") { }
        public string Name { get; }
        // IContainerRegistry + Build
    }
}
```

Дочерний скоуп — только `container.CreateChild()`, не `new ContainerBuilder()`.

Инжекторы сгенерированного кода регистрируются так (A реализует в `Runtime/**`, D эмитит вызовы):

```csharp
namespace Internal {
    public static class ContainerInjectors
    {
        public static void Register(Type implementation, Func<int[], IInjector> factory);
        public static void MarkAssemblyCovered(System.Reflection.Assembly assembly);
    }
}
```

Сгенерированный на сборку класс: `ContainerInjectors_{Assembly}` с методом `Register()`, из `[RuntimeInitializeOnLoadMethod]` зовёт `ContainerInjectors.Register` по каждому типу и `MarkAssemblyCovered(typeof(ContainerInjectors_{Assembly}).Assembly)`.

---

## Как работает план резолва

Это ядро задачи, трек A.

**Build:**
1. Собрать все `IServiceRegistration` скоупа.
2. Каждой выдать `int slot`. Слоты родителя скопировать в карту дочернего (уплощение).
3. Построить `Type → slot` (одна запись на каждый `As<T>()` + `AsSelf()`). Коллизия — последняя регистрация побеждает, как сейчас в VContainer.
4. Для каждого слота разрешить зависимости `Construct`-метода/конструктора в `int[] argSlots`. Не нашлось — ошибка Build с именем типа и параметра.
5. Топологическая сортировка. Цикл — ошибка Build с путём.
6. Предпосчитать `ResolveAll<T>` массивы для всех `T`, на которые есть больше одной регистрации.
7. Сконструировать `Singleton` и `Scoped` в порядке из п.5 в `object[] instances`.

**Resolve:**
```csharp
public object Resolve(Type type) => _instances[_slots[type]];   // Singleton/Scoped
```
`Transient` — вызов инжектора с тем же `argSlots`, без записи в `_instances`.

**Dispose:** обход `BuildOrder` в обратном порядке.

Ожидаемый выигрыш относительно VContainer: убираются проход по цепочке скоупов, второй лукап `Exists`, `ConcurrentDictionary`, `Lazy`, скан `parameters`, аллокации замыканий. Остаётся один лукап `Type → slot` на внешний `Resolve` и ноль лукапов на внутренние рёбра графа. Цифры даёт трек E — **до бенчмарка выигрыш не заявлять**.

---

## Параллельная работа: 7 агентов

Шаг 0 делает оркестратор **в одиночку**. После него треки A–F идут параллельно и не блокируют друг друга. Шаг 6 снова оркестратор.

### Владение файлами (не пересекается)

| Трек | Владеет | Не трогает |
|---|---|---|
| 0 Оркестратор | `Tools/Container/Abstract/**` | — |
| A Ядро | `Tools/Container/Runtime/**` | `Abstract/**` после шага 0 |
| B Тесты | `Internal/Tests/Editor/Container/**` | всё остальное |
| C Дебагер | `Internal/Editor/Tools/Container/**` | рантайм |
| D Генератор | `client/Tools~/ContainerGenerator/**` (D1), `Internal/Editor/Tools/ContainerCodegen/**` (D2), `Tools/Container/Generated/**` | рантайм |
| E Бенчмарк | `Internal/Tests/Editor/Container/Benchmarks/**` | продакшн-код |
| F Граф-вьюер | `Internal/Editor/Tools/ContainerGraph/**` | `Container/**` трека C, рантайм |
| 6 Миграция | `Runtime/Scopes/**`, игровой код | — |

Правка чужого каталога — только через оркестратора. Изменение `Abstract/**` после шага 0 — только через оркестратора, с уведомлением всех треков.

---

## План реализации

#### 0 Contract freeze
- **Статус:** [x] completed
- **Агент:** оркестратор (один, остальные ждут)
- **Цель:** `Tools/Container/Abstract/**` компилируется; все интерфейсы из раздела API существуют; заглушки бросают `NotImplementedException`. `Internal.asmdef` собирается.
- **Как:** Написать файлы один-к-одному с разделом «API (frozen contract)». Ничего не реализовывать. Добавить `ContainerThread` с `Assert()` под `UNITY_EDITOR || DEBUG`.
- **Проверка:** `Internal.dll` компилируется. Ни одного `TODO` в сигнатурах. Ни одной реализации кроме `throw`.
- **Файлы:** `client/Assets/Common/Internal/Runtime/Tools/Container/Abstract/**` [новые]
- **Зависит от:** —
- **Блокирует:** 1, 2, 3, 4, 5

#### 1 Ядро контейнера (трек A)
- **Статус:** [x] completed
- **Агент:** A
- **Цель:** `IContainer` работает целиком на рефлексивном инжекторе: реестр, слоты, уплощение родителя, топосорт, `ResolveAll`, диспоз в обратном порядке, `IContainerDiagnostics`.
- **Как:** Раздел «Как работает план резолва». Рефлексивный `ReflectionInjector` ищет `Construct` по имени и публичный конструктор с наибольшей арностью. `ContainerRegistryDebug` наполняется под редактором.
- **Проверка:** Цикл даёт ошибку на `Build()` с путём, не `StackOverflow`. Отсутствующая зависимость — ошибка на `Build()`, не на резолве. `ResolveAll<T>` без регистраций возвращает пустой список. Диспоз идёт в обратном порядке Build. `ContainerThread.Assert` ловит резолв не с главного потока.
- **Файлы:** `Tools/Container/Runtime/**` [новые]
- **Зависит от:** 0
- **Блокирует:** 6

#### 2 Тесты (трек B)
- **Статус:** [/] in_progress
- **Агент:** B
- **Цель:** Edit-mode NUnit-набор на семантику контейнера. Пишется **против заглушек**, красный до готовности шага 1.
- **Как:** `Internal.Tests` уже существует (`Common/Internal/Tests/Editor/Internal.Tests.asmdef`, nunit, `UNITY_INCLUDE_TESTS`). Стиль — `ModifiableListTests.cs`. Покрыть: три lifetime, `As`/`AsSelf`/коллизия сервисных типов, `WithParameter`, дочерний скоуп и уплощение, `ResolveAll` порядок и пустота, `Transient` не кэшируется, `Scoped` кэшируется на скоуп, цикл, missing dependency, порядок диспоза, `AddInjection`, `Construct` с нулём параметров, `Construct` отсутствует.
- **Проверка:** Каждый locked decision 2–8 имеет тест. Тесты не зовут VContainer. Запуск: Unity Test Runner, EditMode, категория `Container`.
- **Файлы:** `Common/Internal/Tests/Editor/Container/**` [новые]
- **Зависит от:** 0
- **Блокирует:** 6

#### 3 Дебагер сборки (трек C)
- **Статус:** [/] in_progress
- **Агент:** C
- **Цель:** `Tools/Container Debugger` — UI Toolkit окно: дерево контейнеров, таблица регистраций, граф зависимостей выбранного сервиса, порядок Build, история резолвов при включённом тумблере.
- **Как:** Копировать устройство `ProfilerTraceWindow` (`Common/Internal/Editor/Tools/Profiling/`) — `EditorWindow` + `UnityEngine.UIElements`, `.uss` рядом как в `ProjectToolsWindow.uss`. Данные только через `ContainerRegistryDebug` / `IContainerDiagnostics`. Обновление по `Changed` + при смене плей-мода.
- **Проверка:** Окно открывается без запущенной игры и показывает пустое состояние, не исключение. Ничего не резолвит и не конструирует — только читает. Работает на заглушках шага 0 (пустой список рутов).
- **Файлы:** `Common/Internal/Editor/Tools/Container/**` [новые — `Internal.Editor.csproj`]
- **Зависит от:** 0
- **Блокирует:** —

#### 3b Граф-вьюер контейнеров (трек F)
- **Статус:** [x] completed
- **Агент:** F
- **Цель:** Живое дерево контейнеров в Graph Toolkit. Клик по контейнеру показывает: (1) зависимости извне (`RegistrationInfo.IsExternal`), (2) сервисы этого контейнера, (3) ассеты из `LoadAssetGroup` (`LoadedAssets`).
- **Как:** Graph Toolkit уже в редакторе как модуль `UnityEditor.GraphToolkitModule` (сборка `Unity.GraphToolkit.Editor`) — пакет `com.unity.graphtoolkit` **не** ставить, он конфликтует с модулем Unity 6.4+. Namespace `Unity.GraphToolkit.Editor`. Меню `Tools/Container Graph`. Данные только из `ContainerRegistryDebug` / `IContainerDiagnostics`. Это редакторное окно с **рантайм-данными** (play mode / живые Roots), не HUD в билде: у GTK нет runtime backend. Не коммитить `.containergraph` ассеты как способ смотреть дерево — граф строится из живых Roots. Не пересекаться с UI Toolkit окном трека C.
- **Проверка:** Окно открывается без play mode, пустое состояние, не исключение. После Build тестового контейнера видны узлы parent→child. Клик показывает три списка. `Changed` — `IViewableDelegate`, не `event Action`.
- **Файлы:** `Common/Internal/Editor/Tools/ContainerGraph/**` [новые]
- **Зависит от:** 0
- **Блокирует:** —

#### 4 Генератор инжекторов (трек D)
- **Статус:** [x] completed
- **Агент:** D
- **Цель:** Roslyn incremental generator: для типов с `Construct` и/или публичным конструктором эмитит `{Type}GeneratedInjector : IInjector` и на сборку — `ContainerInjectors_{Assembly}` со словарём `Type → Func<IInjector>`.
- **Как:** `IIncrementalGenerator`, netstandard2.0, dll помечается меткой `RoslynAnalyzer` и выключается на всех платформах в `.meta`. Двухстадийный пайплайн: символы → value-equatable модель → emit, `Location` вне equality (иначе правка строки сверху файла инвалидирует кэш). Референс: `VContainer.SourceGenerator` в апстриме — устройство пайплайна оттуда, поведение отсюда.
- **Проверка:** Сгенерированный `Create` вызывает `new` напрямую, `Construct` — прямым вызовом, аргументы берутся `plan.Get<T>(slot)`. Ни одного `Activator.CreateInstance`, ни одного `Assembly.GetType`. Generic и nested типы не покрываются — диагностика, не молчание. Отключение генератора не ломает сборку (locked 9).
- **Файлы:** `client/Tools~/ContainerGenerator/**` [новые], выходной dll в `Assets/Plugins/ContainerGenerator/`
- **Зависит от:** 0
- **Блокирует:** —

#### 4b Анализ графа регистраций (трек D, после 4)
- **Статус:** [x] completed
- **Агент:** D
- **Цель:** Разметка графа скоупа на этапе компиляции: от точки входа обойти installer'ы и собрать, что регистрируется. Выход — описание графа, которое шаг 1 использует вместо рантайм-сборки плана. Инжекторы (шаг 4) от него не зависят.
- **Как:** Два механизма, потому что Roslyn-генератор не видит ассеты (`Compilation` + `AdditionalFiles`, префабы Unity ему не отдаёт):
  - **D1, Roslyn.** Корень — метод, помеченный атрибутом (`Construct` / `Build`, **не** класс: `GlobalScopeExtensions` содержит ещё профайлерную обвязку и `LoadPrefabGroup`). Обход: `static ... AddX(this IScopeBuilder)` рекурсивно.
  - **D2, editor-codegen (`AssetPostprocessor`).** Читает ассет, перечисляет типы компонентов, дописывает `partial` того же класса. Прецедент — `partial class Prefabs` из `prefab_catalog`. Симметричен для двух источников:

    | | Ассет | Держатель | Хук |
    |---|---|---|---|
    | Сущности | `.prefab` | `ScopeEntityView._register` / `_autoDetected` | `IEntityComponent.Register(IEntityBuilder)` |
    | Сцены | `.unity` | `SceneServicesFactory._services` | `ISceneService.Create(IScopeBuilder)` |

    Дальше типы из ассета уходят в тот же обход D1: их `Register`/`Create` — обычные installer-методы.
- **Что обход обязан понимать** (всё встречается в проекте, ссылки — реальные места):
  1. Лямбда-аргументы: `services.Measure("Updater", () => builder.AddUpdater())` — `GlobalScopeExtensions.cs`. Прямым `InvocationExpression` регистрация не видна.
  2. Fluent-цепочки регистрации: `RegisterComponent(x).As<IUpdater>().AsSelfResolvable()`, и цепочки installer'ов: `.AddCardLocalComponents().AddCardLocalRoot().AddCardLocalStates()` — `CardFactory.cs`.
  3. `builder.Instantiate(prefab)` → локальная переменная → `RegisterComponent(эта переменная)` — `GlobalUpdaterExtensions.cs`. Слот помечается «приходит из инстанцирования префаба». Отдельная метка руками не нужна, `ScopeBuilderExtensions.Instantiate` — единственная точка на проект.
  4. `RegisterComponent(GlobalPrefabs.GlobalAudioPlayer)` **без** `Instantiate` — регистрируется сам ассет префаба. Отличать от п.3.
  5. `switch` по замкнутому enum, где арки — `Register<T>()`: `CardStatesExtensions.AddCardAction`, 78 арок. Вариативность заперта в одном слоте, `2^N` не возникает.
  6. `if/else` по рантайм-значению, обе ветки регистрируют один сервисный тип — `GlobalPublisherExtensions.cs:47`.
  7. `RegisterInstance(значение)` / `WithParameter(значение)`, где значение рантайм-ное, а статический тип известен — становится параметром сгенерированного класса («дыркой»), не выводится.
- **Locked:** для случая 5 генерируется **фабричный метод** (`ICardAction CreateCardAction(CardType, CardConfigOptions)` со switch внутри, арки — `new CardXAction(слоты, configs.X)`), а не 78 специализаций класса скоупа. Причина: следом за первым switch идёт второй по той же переменной с `WithParameter`, и корреляция двух switch — хрупкая. Форма switch сохраняется, специализируется только резолв зависимостей.
- **Проверка:** Непокрытая конструкция — **ошибка генерации с файлом и строкой**, не молчаливый пропуск (locked 10). `GlobalScopeExtensions.LoadGlobal` и `CardFactory.Build` разбираются полностью. Scene-сервисы (15 `ISceneService`) не выпадают — D2 покрывает и `.unity`. Отключение шага 4b не ломает сборку: шаг 1 строит план в рантайме.
- **Файлы:** `client/Tools~/ContainerGenerator/**`, `Common/Internal/Editor/Tools/ContainerCodegen/**` [новые — `Internal.Editor.csproj`]
- **Зависит от:** 0
- **Блокирует:** —

#### 5 Бенчмарк (трек E)
- **Статус:** [/] in_progress
- **Агент:** E
- **Цель:** Один граф, две реализации, отчёт в `docs/tasks/current/own_di/benchmark.md`.
- **Как:** Синтетический граф той же формы, что боевой (≈30 сервисов, глубина 3 скоупа, 12 маркерных интерфейсов через `ResolveAll`). Мерить отдельно: Build, первый полный `ResolveAll` всех 12 фаз, 10k `Resolve<T>` синглтона из глубокого скоупа, 10k `Transient`, аллокации через `GC.GetAllocatedBytesForCurrentThread`. Сторона VContainer пишется первой — она не зависит от шага 1.
- **Проверка:** Обе стороны строят идентичный граф. Прогрев отдельно от замера. В отчёте — сырые числа и железо, без округлений в свою пользу.
- **Файлы:** `Common/Internal/Tests/Editor/Container/Benchmarks/**` [новые], `docs/tasks/current/own_di/benchmark.md`
- **Зависит от:** 0 (сторона VContainer), 1 (сторона нового контейнера)
- **Блокирует:** 6

#### 6 Миграция и удаление VContainer
- **Статус:** [~] перенесён в `di_scope_codegen`, шаг 4 (решение пользователя, 2026-09-10)
- **Почему:** переводить боевой код на слотовый рантайм-план, а затем второй раз на сгенерированный класс скоупа — двойная миграция. Мигрируем один раз, сразу на конечную форму. Детали ниже остаются в силе как содержание работы, исполняется она в другой задаче.
- **Агент:** оркестратор
- **Цель:** `Internal`-скоупы работают на своём контейнере, `Assets/Plugins/VContainer` удалён, игровой код не изменился по смыслу.
- **Как:** По порядку: `IServiceCollection`/`IRegistration` → свои типы; `InstanceInjection`/`ServiceCollection.Resolve` → `IContainer`; `ViewInjector` → `IContainer.Inject`; `LifetimeScope` в `IScopeEntityView`/`CardScope`/`GamePlayerScope`/фабриках → свой `ScopeEntityView` без наследования от VContainer; `EventLoop.ResolveList<T>` → `IContainer.ResolveAll<T>`; `CardActionSyncDispatcher(IObjectResolver)` → `IContainer`; `BuilderExtensions` дефолты → `ServiceLifetime`; 35 `[Inject]`-полей → `Construct`; мёртвые `using VContainer.Internal` снести.
- **Проверка:** `grep -rn "VContainer" client/Assets --include="*.cs"` = 0. Плей-мод: меню грузится, матч против бота стартует, карты и поле 16×16 инстансятся, скоуп карты создаётся и диспозится. Бенчмарк переснят на боевом графе.
- **Файлы:** `Runtime/Scopes/**`, `Common/Flow/Startup/**`, `GamePlay/Cards/**`, `GamePlay/Players/**`, `Assets/Plugins/VContainer` (delete)
- **Зависит от:** 1, 2, 5
- **Блокирует:** —

---

## Гейт: генерируемый класс скоупа

Шаг 4b даёт **описание графа**, но план по-прежнему собирается в рантайме (шаг 1). Схема «один сгенерированный класс на скоуп, все сервисы полями, MonoBehaviour приходят извне» — эмит этого описания как полей и `new` вместо плана — **не в этой задаче**.

**Гейт снят 2026-09-10 решением пользователя.** Заведена отдельная задача: `docs/tasks/current/di_scope_codegen/`. Она стартует после закрытия `own_di` и переделывает эмит; бенчмарк шага 5 больше не является условием её открытия.

Внутри `own_di` ничего не меняется: шаг 4 доводится до конца в текущем виде (`IInjector` + `IResolvePlan`), потому что рантайм-план остаётся фолбэком и после переделки (`di_scope_codegen`, locked 12).

Что под неё уже заложено:
- 4b — вся статическая часть анализа.
- `IEntityComponent.Register(IEntityBuilder)` и `ISceneService.Create(IScopeBuilder)` — компонент зовёт себя сам, его статический тип известен в точке вызова. Туда ложится двойной диспатч `IProvides<T>` без правки рантайма. Без этого перегрузки `Inject(MonoServiceA)` не разрешаются: при итерации по `[SerializeField] MonoBehaviour[]` статический тип — `MonoBehaviour`.
- Каталог префабов (`prefab_catalog`, complete) — типизированные `GlobalPrefabs.GlobalUpdater` / `GamePlayPrefabs.CardLocal` дают статический путь от выражения в коде до asset GUID. Без него связка «код ↔ ассет» не строится вообще.

---

## Ключевые файлы

| Файл | Роль |
|------|------|
| `Runtime/Scopes/Common/Builders/BuilderExtensions.cs` | Поверхность регистрации, не меняется |
| `Runtime/Scopes/Common/Builders/IBuilder.cs` | `Services` / `Events` / `Lifetime` |
| `Runtime/Scopes/Common/Services/ServiceCollection.cs` | Мост на VContainer, переписывается в шаге 6 |
| `Runtime/Scopes/Common/Events/EventLoop.cs` | 12 `ResolveList<T>` на скоуп — горячая точка |
| `Runtime/Scopes/Entities/EntityScopeLoader.cs` | Сборка сущностного скоупа |
| `Runtime/Scopes/Entities/ScopeEntityView.cs` | Регистрации из данных префаба |
| `Runtime/Scopes/Common/Services/ViewInjector.cs` | `LifetimeScope.Container.Inject` |
| `Editor/Tools/Profiling/ProfilerTraceWindow.cs` | Образец UI Toolkit окна для трека C |
| `Tests/Editor/ModifiableListTests.cs` | Образец стиля тестов для трека B |
| `Assets/Plugins/VContainer/Runtime/Internal/InjectorCache.cs` | Как **не** надо искать инжектор |

## Документация к прочтению

- `.agents/docs/COMMON_CONTAINER.md` — текущий контракт скоупов; обновить в шаге 6
- `.agents/docs/COMMON_LIFETIMES.md`, `COMMON_LIFETIMES_PATTERNS.md` — `ILifetime` остаётся как есть
- `.agents/docs/CODE_STYLE_FULL.md` — скобки на той же строке, no `Async` suffix, коллекции не null
- `.agents/docs/API_DESIGN_FULL.md` — форма публичного API
- `.agents/docs/CLAUDE_MISTAKES.md` — циклы сборок

## Риски

- **Расхождение треков по контракту.** Единственная защита — шаг 0 и запрет править `Abstract/**`. Если трек упирается в нехватку API, он останавливается и идёт к оркестратору, а не дописывает интерфейс у себя.
- **Уплощение родителя и время жизни.** Копирование слотов родителя в дочерний скоуп корректно, только пока родитель живёт дольше ребёнка. Проверить на `CardScope`, который создаётся и умирает внутри матча.
- **`ResolveAll` и порядок.** Сейчас порядок фаз в `EventLoop` де-факто задан порядком регистрации. Предпосчитанный массив обязан его сохранить, иначе поедет порядок `OnSetup` у 36 сервисов и это будет тихо.
- **`ScopeEntityView` и оверрайды сцены.** Регистрации приходят из `[SerializeField] Component[]`. План строится в рантайме после `CreateViews` — порядок вызовов в `EntityScopeLoader` менять нельзя.
- **Бенчмарк без прогрева** покажет JIT/домен-релоад вместо контейнера. Прогрев обязателен и отдельным проходом.
- **Стриппинг.** Явная регистрация инжекторов через `ContainerInjectors_{Assembly}` снимает проблему, но только если этот класс реально вызывается из `[RuntimeInitializeOnLoadMethod]`, а не найден рефлексией.

## Explicitly forbidden

- Менять сигнатуры `IBuilder` / `BuilderExtensions` / `IEventLoop` / `ILifetime`
- `event Action` / `event Action<T>` — locked 15, брать `IViewableDelegate`
- Атрибуты для регистрации или инъекции (`[Inject]`, `[Service]`, `[Singleton]`) — locked 8
- `Assembly.GetType` / `Activator.CreateInstance` для поиска инжектора
- Молчаливый фолбэк на рефлексию при помеченной сборке — locked 10
- `ConcurrentDictionary`, `Lazy<T>`, блокировки в рантайме резолва — locked 2
- Ленивый резолв по умолчанию — locked 5
- Подъём по цепочке родителей в рантайме — locked 4
- Правка чужого каталога трека без оркестратора
- Удаление `Assets/Plugins/VContainer` раньше шага 6
- Генерируемый класс скоупа (см. «Гейт») — отдельная задача
- Заявлять выигрыш по перфу до отчёта шага 5
