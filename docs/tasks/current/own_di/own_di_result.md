## Own DI Container — Результат

### Статус: В работе (A–F написаны; шаг 6 ждёт зелёных тестов и бенчмарка own-стороны)

### Что сделано

- Заглушки контракта в `Tools/Container/Abstract/**`: 12 типов в namespace `Internal`, `Internal.dll` собирается. `IContainer.Lifetime` — `IReadOnlyLifetime`.
- Abstract утверждён. Handshake: живой `ContainerRegistryDebug`, `ContainerBuilder`, VContainer в `Internal.Tests.asmdef`.
- Трек A: ядро контейнера в `Runtime/**`.
- Трек B: 42 EditMode теста категории `Container`.
- Трек C: `Tools/Container Debugger`.
- Трек E: VContainer-цифры в `benchmark.md`; own ждёт прогона.
- Трек D: генератор инжекторов + 4b walker/D2. Dll в `Plugins/ContainerGenerator`.
- Трек F: `Tools/Container Graph` (Graph Toolkit).

### Измененные файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|
| `Tools/Container/Abstract/ServiceLifetime.cs` | enum Transient/Scoped/Singleton | 0 | тип в Internal.dll |
| `Tools/Container/Abstract/IContainerRegistry.cs` | Add/AddInstance/AddComponent/AddInjection/AddSelfResolvable | 0 | тип в Internal.dll |
| `Tools/Container/Abstract/IServiceRegistration.cs` | As/AsSelf/WithParameter | 0 | тип в Internal.dll |
| `Tools/Container/Abstract/IContainer.cs` | Diagnostics/Lifetime, Resolve/TryResolve/ResolveAll/Inject/CreateChild | 0 | тип в Internal.dll |
| `Tools/Container/Abstract/IContainerBuilderScope.cs` | : IContainerRegistry + Build | 0 | тип в Internal.dll |
| `Tools/Container/Abstract/IInjector.cs` | Create/Construct | 0 | тип в Internal.dll |
| `Tools/Container/Abstract/IResolvePlan.cs` | Get/Get&lt;T&gt; | 0 | тип в Internal.dll |
| `Tools/Container/Abstract/IContainerDiagnostics.cs` | Name/Parent/Children/Registrations/BuildOrder/History | 0 | тип в Internal.dll |
| `Tools/Container/Abstract/RegistrationInfo.cs` | readonly struct + ctor по полям | 0 | тип в Internal.dll |
| `Tools/Container/Abstract/ResolveRecord.cs` | readonly struct + ctor по полям | 0 | тип в Internal.dll |
| `Tools/Container/Abstract/ContainerRegistryDebug.cs` | живые Roots/Changed, internal AddRoot/RemoveRoot | 0 | тип в Internal.dll |
| `Tools/Container/Abstract/ContainerThread.cs` | Assert() под UNITY_EDITOR \|\| DEBUG | 0 | тип в Internal.dll |
| `Tools/Container/Runtime/ContainerBuilder.cs` | throw-заглушка `IContainerBuilderScope`, имя заморожено | 0→1 | A заменяет |
| `Internal/Tests/Editor/Internal.Tests.asmdef` | reference `VContainer` для трека E | 5 | — |
| `Tools/Container/Runtime/*.cs` | ядро: билдер, слоты, flatten, ReflectionInjector, диагностика | 1 | `Internal.ContainerBuilder` в Internal.dll |
| `Internal/Editor/Tools/ContainerGraph/**` | Graph Toolkit live tree, `Tools/Container Graph` | 3b | console errors = 0 |

### Проверки

| Проверка | Шаг | Результат |
|----------|-----|-----------|
| `grep -rn "VContainer" client/Assets --include="*.cs"` = 0 | 6 | — |
| EditMode тесты категории `Container` зелёные | 2 | — |
| Цикл в графе → ошибка Build с путём | 1 | — |
| Missing dependency → ошибка Build, не резолва | 1 | — |
| Порядок диспоза обратен порядку Build | 1 | — |
| Порядок `ResolveAll<T>` совпал с порядком регистрации | 1 | — |
| Ни одного `Activator.CreateInstance` / `Assembly.GetType` в поиске инжектора | 4 | — |
| `GlobalScopeExtensions.LoadGlobal` разобран обходом без ошибок генерации | 4b | — |
| `CardFactory.Build` разобран (обе ветки + switch на 78 арок) | 4b | — |
| Scene-сервисы не выпали: D2 покрывает `.unity`, 15 `ISceneService` в графе | 4b | — |
| Непокрытая конструкция даёт ошибку с файлом и строкой, не молчание | 4b | — |
| Плей-мод: меню + матч против бота + поле 16×16 | 6 | — |
| Бенчмарк переснят на боевом графе | 6 | — |

### Бенчмарк

Результаты — `benchmark.md`. Заполняется шагом 5, пересдаётся после шага 6.

### Не сделано / отложено

- Генерируемый класс на скоуп (все сервисы полями, MonoBehaviour извне) — отдельная задача, открывается только по результатам бенчмарка. См. «Гейт» в `own_di_info.md`.
