---
task: di_scope_codegen
status: pending
phase: implementation
created: 2026-09-10
updated: 2026-09-10
total_steps: 5
completed_steps: []
blocked_steps: []
agents: 4
depends_on: own_di
---

# Scope codegen — spec

Authoritative contract. If this file and the code disagree, this file wins. Do not invent API that is not written here.

Repo root: `/projects/mines-leader`
Unity project: `/projects/mines-leader/client`
Предшественник: `docs/tasks/current/own_di/own_di_info.md` — **дочитать целиком перед этой задачей.**

Задача стартует только после того, как `own_di` закрыт. Она переделывает способ эмита, не переписывая контейнер заново.

---

## Что я хочу

Сейчас сгенерированный код выглядит так (`AchievementRowGeneratedInjector`):

```csharp
public object Create(IResolvePlan plan) {
    var instance = new AchievementRow(
        plan.Get<InGameAchievementType>(_slot0),
        plan.Get<string>(_slot1),
        plan.Get<IReadOnlyList<IAchievementTier>>(_slot2));
    ...
}
```

Это лучше VContainer по константе, но **той же формы**: дерево зависимостей собирается в рантайме, каждое ребро — интерфейсный вызов `IResolvePlan.Get`, проверка границ, чтение массива, проверка на null, каст.

Хочу вместо этого класс, в котором дерево записано построчно:

```csharp
var configs = new MatchMakingConfigs();
var registry = new GameModesRegistry(configs);
var matchmaking = new Matchmaking(configs, registry, _updater);
```

Внутренние рёбра — чтение локали или поля. `Dictionary<Type, object>` заполняется **в конце**, и он нужен только для внешнего `IContainer.Resolve<T>()`, а не как механизм резолва.

---

## Цель №1 — компайл-тайм валидация, не перф

Главный выигрыш не в скорости. Он в том, что **нет регистрации — класс не генерируется, сборка красная**. Цикл в графе — ошибка компиляции. Сегодня и то и другое — исключение в рантайме на старте сцены.

Формулировка приоритета нужна для спорных решений: если выбор между «быстрее» и «ошибка ловится на компиляции» — выигрывает второе.

Перф — следствие, и его меряет шаг 4, а не аргументация.

---

## Что уже готово и служит входом

Из `own_di`, ничего из этого не переписывается с нуля:

| Что | Где | Роль в этой задаче |
|---|---|---|
| `GraphWalker` (~1125 строк) | `client/Tools~/ContainerGenerator/Graph/` | обход installer'ов; **вход задачи** |
| `GraphDocument` / `GraphRegistration` | `Graph/GraphModel.cs` | модель «что зарегистрировано» |
| `CINGR001` / `CINGR002` | `Graph/GraphDescriptors.cs` | уже `DiagnosticSeverity.Error` |
| `TypeAnalyzer` | `Injector/TypeAnalyzer.cs` | «что нужно каждому типу» |
| Рантайм-контейнер на слотах | `Tools/Container/Runtime/` | становится **фолбэком**, не удаляется |
| `Abstract/**` | `Tools/Container/Abstract/` | не меняется, генерируемый класс реализует тот же `IContainer` |
| Тесты, дебагер, бенчмарк | по трекам `own_di` | расширяются, не переписываются |

### Ядро задачи: соединить две половины генератора

Сейчас они независимы и **ни одна в одиночку не может эмитить класс скоупа**:

- `Graph/` знает, *что* зарегистрировано, но `GraphRegistration` не содержит рёбер — у него нет поля под параметры конструктора.
- `Injector/` знает, *что нужно* каждому типу, но не знает, что из этого зарегистрировано.

Переделка = соединить их, разрешить каждое ребро в конкретную регистрацию, отсортировать топологически, эмитить код. Всё остальное в задаче — следствия.

---

## Locked decisions

1. **`Dictionary<Type, object>` — экспортная таблица, а не механизм резолва.** Заполняется один раз в конце конструирования. Внутренние рёбра графа в него не ходят никогда.
2. **`ResolveAll<T>` — отдельные поля-массивы**, собранные при построении, а не выборка из словаря. Порядок обязан совпасть с порядком регистрации (12 маркерных интерфейсов, 36 реализаторов `IScopeSetup` — порядок `OnSetup` поедет тихо).
3. **Скоуп генерируется целиком или не генерируется вовсе.** Частично сгенерированного скоупа не бывает: смешивать поля и `IResolvePlan` внутри одного графа запрещено.
4. **Отсутствующая регистрация и цикл — ошибка компиляции.** Не диагностика-предупреждение, не рантайм. Это и есть фича.
5. **Непокрытый синтаксис — тоже ошибка компиляции** (`CINGR001` остаётся `Error`). Осознанный отказ от генерации оформляется явно — атрибутом `[ContainerRuntimeScope]` на корне, и тогда скоуп идёт на рантайм-план. Молчаливого фолбэка нет ни в одном виде.
6. **Lifetime → форма хранения:** `Singleton` и `Scoped` — поле, конструируется в топологическом порядке в конструкторе. `Transient` — приватный метод `Create{Type}()`, поля нет.
7. **Дырки — параметры конструктора генерируемого класса.** Всё, что `GraphWalker` пометил как `InstanceHole`, `ParameterHole`, `PrefabInstance`, `PrefabAsset`, `SceneServices`, приходит извне и типизировано.
8. **`switch` по замкнутому enum → фабричный метод**, не специализация класса. Подтверждается решение из `own_di` (`AddCardAction`, 78 арок, следом второй switch с `WithParameter`).
9. **`Dispose` генерируется в порядке, обратном конструированию.** Явным списком, без `CompositeDisposable`.
10. **Генерируемый класс реализует `IContainer`** из `Abstract/**`. Игровой код и `IBuilder`/`BuilderExtensions`/`IEventLoop` не меняются.
11. **Кросс-сборочные installer'ы решаются манифестом в assembly-атрибутах.** Roslyn не видит тела методов из ссылочных сборок, но `AttributeData` из метаданных читает полностью. Каждая сборка эмитит описание своих installer'ов сама (тело ей видно), потребитель читает его через `ReferencedAssemblySymbols` → `GetAttributes()`. Символы типов через границу проходят как обычно, поэтому сигнатуры конструкторов в манифест не входят — только что регистрируется, с каким lifetime, в каком порядке и какие дырки. Installer, не покрытый ни локальным обходом, ни манифестом, — `CINGR002`, ошибка.
12. **Per-type `IInjector` для покрытых скоупов не генерируется вообще.** Их конструирует класс скоупа. `IInjector` остаётся только для фолбэка.

---

## Форма генерируемого класса

Для скоупа с корнем `Global.Setup.GlobalScopeExtensions.LoadGlobal`:

```csharp
namespace Global.Setup {
    internal sealed class GlobalScopeContainer : global::Internal.IContainer {
        // 7 дырки: то, что приходит извне
        public GlobalScopeContainer(
            global::Internal.IUpdater updater,              // PrefabInstance: builder.Instantiate(GlobalPrefabs.GlobalUpdater)
            global::Global.Audio.AudioPlayer audioPlayer,   // PrefabAsset:    RegisterComponent(GlobalPrefabs.GlobalAudioPlayer)
            bool isEditor)                                  // Alternative:    if (platformOptions.IsEditor)
        {
            _updater = updater;
            _audioPlayer = audioPlayer;

            // построчно, в топологическом порядке
            _delayRunner   = new global::Internal.DelayRunner(_updater);
            _currentCamera = new global::Global.Cameras.CurrentCamera();
            _cameraUtils   = new global::Global.Cameras.CameraUtils(_currentCamera);
            _itchSaves     = new global::Global.Publisher.ItchSaves();
            _languageApi   = isEditor
                ? (global::Global.Publisher.IItchLanguageAPI)new global::Global.Publisher.ItchLanguageDebugAPI()
                : new global::Global.Publisher.ItchLanguageExternAPI();

            // Construct-фаза, тоже построчно
            _someService.Construct(_delayRunner, _cameraUtils);

            // предпосчитанные маркерные списки
            _scopeSetup = new global::Internal.IScopeSetup[] { _audioPlayer, _someService };

            // экспортная таблица — последним действием
            _exports = new global::System.Collections.Generic.Dictionary<global::System.Type, object>(12) {
                { typeof(global::Internal.IUpdater),      _updater },
                { typeof(global::Internal.IDelayRunner),  _delayRunner },
                ...
            };
        }

        private readonly global::Internal.IUpdater _updater;
        ...
        private readonly global::System.Collections.Generic.Dictionary<global::System.Type, object> _exports;

        public object Resolve(global::System.Type type) {
            if (_exports.TryGetValue(type, out var instance) == true)
                return instance;
            throw new global::Internal.ContainerException(...);
        }

        // Transient — метод, не поле
        private global::Global.UI.Popup CreatePopup() => new global::Global.UI.Popup(_cameraUtils);

        public void Dispose() {
            // обратный порядок конструирования, явным списком
            _itchSaves.Dispose();
            _cameraUtils.Dispose();
            ...
        }
    }
}
```

Имя класса — `{RootTypeName}{RootMethodName}Container`, в неймспейсе корня, `internal sealed`.

---

## Двойной диспатч для компонентов префаба и сцены

`ScopeEntityView._register` — `Component[]`, `SceneServicesFactory._services` — `MonoBehaviour[]`. При итерации статический тип — `Component`/`MonoBehaviour`, и перегрузка `Construct(CellVisuals)` **не разрешится**: C# выбирает перегрузку по статическому типу на компиляции.

Точки обратного вызова уже есть в коде и менять их не надо:

- `IEntityComponent.Register(IEntityBuilder)` — вызывается из `ScopeEntityView.CreateViews`
- `ISceneService.Create(IScopeBuilder)` — вызывается из `SceneServicesFactory.Create`

Внутри этих методов `this` имеет точный статический тип. Туда ложится:

```csharp
public interface IProvides<T> { void Construct(T target); }

// в компоненте
public void Register(IEntityBuilder builder) => builder.Provide(this);  // перегрузка выбрана на компиляции
```

Генерируемый класс скоупа реализует `IProvides<T>` для каждого компонента, который даёт ассет. Констрейнт проверяется компилятором: скоуп, не умеющий этот компонент, — ошибка сборки, а не рантайма.

---

## Параллельная работа: 4 агента

Оркестратор + 3 трека. Шаг 0 блокирующий, дальше параллель.

| Трек | Владеет | Шаг |
|---|---|---|
| 0 Оркестратор | `Graph/GraphModel.cs` (расширение модели рёбрами) | 0 |
| G Эмиттер | `client/Tools~/ContainerGenerator/Scope/**` | 1 |
| R Рантайм и интеграция | `Tools/Container/Runtime/**`, `Runtime/Scopes/**` | 2 |
| T Тесты и бенчмарк | `Internal/Tests/Editor/Container/**` | 3 |
| 0 Оркестратор | миграция корней, снос мёртвого | 4 |

---

## План реализации

#### 0 Модель рёбер (оркестратор, блокирующий)
- **Статус:** [ ] pending
- **Цель:** `GraphRegistration` умеет описать ребро. Резолвер рёбер соединяет `Graph` и `Injector`: для каждой регистрации — список зависимостей, каждая разрешена в конкретную другую регистрацию или в дырку.
- **Как:** Расширить `Graph/GraphModel.cs` полем зависимостей. Написать `Scope/EdgeResolver` — берёт `GraphDocument` + `TypeAnalyzer`, отдаёт граф с рёбрами. Топосорт с детекцией цикла. Не разрешилось — `CINGR003 Error` с типом, параметром, файлом и строкой; цикл — `CINGR004 Error` с полным путём.
- **Проверка:** На `GlobalScopeExtensions.LoadGlobal` и `CardFactory.Build` граф с рёбрами строится целиком. Искусственно снятая регистрация даёт `CINGR003` с точным именем параметра. Искусственный цикл даёт `CINGR004` с путём.
- **Блокирует:** 1, 2, 3

#### 1 Эмиттер класса скоупа (трек G)
- **Статус:** [ ] pending
- **Цель:** Из графа с рёбрами эмитится класс по разделу «Форма генерируемого класса».
- **Как:** `Scope/ScopeEmitter`. Дырки → параметры конструктора. `Singleton`/`Scoped` → поля в топологическом порядке. `Transient` → методы. `switch` по enum → фабричный метод. `Alternative` → тернарник или ветка. Маркерные списки → поля-массивы. Словарь — последним. `Dispose` — обратным списком. `IProvides<T>` для компонентов ассетов.
- **Проверка:** Для `LoadGlobal` класс компилируется и содержит ноль обращений к `IResolvePlan`. Порядок в маркерных массивах совпадает с порядком регистрации. `Transient` не имеет поля. `[ContainerRuntimeScope]` на корне отключает генерацию для этого скоупа и только для него.
- **Зависит от:** 0

#### 1b Кросс-сборочный манифест (трек G, после 1)
- **Статус:** [ ] pending
- **Цель:** Installer, чьё тело лежит в другой сборке, участвует в графе наравне с локальным.
- **Как:** Сменить цель эмита `GraphEmitter` со `static readonly` полей на assembly-атрибут `[assembly: ContainerInstaller(...)]`. Инициализатор массива — это тело статического конструктора, через границу сборки Roslyn его не читает; `AttributeData` читает. Аргументы обязаны быть константами: `typeof()`-массивы под типы (устойчивее строк, корректно резолвят generics) плюс строковый блоб под lifetime / origin / порядок. Потребитель — `compilation.SourceModule.ReferencedAssemblySymbols` → `GetAttributes()`. Данные для манифеста уже собирает `GraphWalker`, новой аналитики не требуется.
- **Проверка:** Три известных кросс-сборочных вызова разбираются полностью и не дают `CINGR002`:
  - `GamePlay/Loop/GamePlayScopeExtensions.cs:66` → `AddSessionServices` (тело в `Internal`)
  - `Meta/MetaScopeExtensions.cs:63` → `AddNetworkConnection` (тело в `Internal`)
  - `GamePlay/Players/Services/Factory/GamePlayerFactory.cs:61` → `AddRemoteEntity` (тело в `Internal`)

  Манифест `Internal` читается из `GamePlay` и из `Meta`. Порядок регистраций в манифесте совпадает с порядком в теле installer'а. Сборка `Internal` компилируется одна, без потребителей.
- **Зависит от:** 1

#### 2 Стык с рантаймом (трек R)
- **Статус:** [ ] pending
- **Цель:** Сгенерированный класс подставляется вместо рантайм-сборки там, где он есть; рантайм-план остаётся для скоупов под `[ContainerRuntimeScope]` и для сущностных скоупов, которые не покрылись.
- **Как:** Точка выбора одна и явная. `IContainerDiagnostics` получает признак «скоуп сгенерирован». Дебагер (`own_di` трек C) показывает его в списке.
- **Проверка:** Плей-мод: меню грузится, матч против бота стартует. Для сгенерированного скоупа `Resolve` не создаёт объектов — только читает словарь. Дебагер отличает сгенерированный скоуп от рантайм-ного.
- **Зависит от:** 0

#### 3 Тесты и бенчмарк (трек T)
- **Статус:** [ ] pending
- **Цель:** Компайл-тайм гарантии проверены тестами; бенчмарк получает третью сторону.
- **Как:** Тесты генератора — на снятой регистрации, цикле, непокрытом синтаксисе, `[ContainerRuntimeScope]`, порядке маркерных массивов. Бенчмарк — третья колонка рядом с VContainer и рантайм-планом, тот же граф.
- **Проверка:** Каждый из locked 3–6 имеет тест. Бенчмарк меряет Build, первый полный проход 12 фаз, 10k `Resolve` из глубокого скоупа, аллокации. Прогрев отдельно.
- **Зависит от:** 0 (тесты генератора), 1 и 2 (бенчмарк)

#### 4 Миграция корней и уборка (оркестратор)
- **Статус:** [ ] pending
- **Цель:** Все боевые корни размечены, мёртвый код снят.
- **Как:** `[ContainerGraphRoot]` на боевых корнях. Снести per-type `IInjector`-эмит для покрытых скоупов (locked 12). Обновить `.agents/docs/COMMON_CONTAINER.md`.
- **Проверка:** Ни один боевой скоуп не падает на `CINGR00*`. Плей-мод зелёный. Бенчмарк переснят на боевом графе.
- **Зависит от:** 1, 2, 3

---

## Риски

- **Две половины генератора расходятся по типам.** `Graph` оперирует строками (`ImplementationType` как `string`), `Injector` — символами. Резолвер рёбер обязан сводить их по одному правилу (`SymbolDisplayFormat.FullyQualifiedFormat`), иначе `IReadOnlyList<IAchievementTier>` из одной половины не совпадёт с таким же из другой.
- **Порядок маркерных массивов.** Тихая поломка: `OnSetup` у 36 сервисов поедет, ничего не упадёт. Тест на порядок — обязателен, а не желателен.
- **Сущностные скоупы.** `CardScope` / `GamePlayerScope` создаются на карту и на игрока и питаются данными префаба. Если они не покроются — основной горячий путь останется на рантайм-плане, и весь выигрыш достанется холодному старту. Проверить их покрытие **до** шага 1, а не после.
- **Манифест — версионируемая поверхность.** Он становится форматом сериализации между двумя прогонами генератора: `Internal` пишет, `GamePlay` читает. Разъехавшийся формат даст не ошибку, а неверный граф. Формат менять только вместе с обеими сторонами; в тестах — кейс «манифест собран старой версией».
- **Дырок может оказаться много.** Конструктор генерируемого класса на 15 параметров — сигнал, что часть регистраций стоит перевести из рантайм-значений в статические, а не повод усложнять генератор.
- **`[ContainerRuntimeScope]` как побег от ошибок.** Атрибут задуман для честных исключений. Если им начнут глушить `CINGR001`, задача потеряет смысл — считать их количество в шаге 4.

## Explicitly forbidden

- Смешивать поля и `IResolvePlan` внутри одного скоупа (locked 3)
- Молчаливый фолбэк на рантайм-план при ошибке генерации (locked 5)
- Ходить в `Dictionary` за внутренними рёбрами графа (locked 1)
- Менять `Abstract/**`, `IBuilder`, `BuilderExtensions`, `IEventLoop`, `ILifetime`
- Удалять рантайм-контейнер на слотах — он фолбэк (locked 12)
- Специализировать класс скоупа под арки `switch` (locked 8)
- Заявлять выигрыш по перфу до отчёта шага 3
