# 04. Ядро клиента (C1-C7)

A = `/projects/peace-data-reborn`, B = этот репозиторий. Пути без префикса даны в B от `client/Assets/Common/`.
Для всех задач нужен редактор B: компиляция, EditMode-тесты, Play Mode. Работать по очереди через `editor_owner.txt`.

Совпадают с A на старте этапа (`0a67a0b08^`), то есть патч накладывается:
- `LoadedScope.cs`, `SceneServicesFactory.cs`, `Updater.cs`;
- `UIStateMachine.cs`, `ItchSaves.cs`;
- `GraphWalker.cs`, `ContainerGraphAssetScanner.cs`.

Отличаются:
- `EventLoop.cs`: в A после форка оптимизирован (ленивые списки, статические лямбды);
- `InputConstraintsStorage.cs` (2 строки);
- `Settings*`;
- `BuilderExtensions.cs`, `SpriteGenerator.cs`, `AudioCatalogGenerator.cs`;
- `MockSwitcher.cs`, `Internal.Editor.asmdef`.

## C1. Скоуп выгружает asset groups после разборки lifetime

- **Приоритет:** P1. **Волна:** 4.
- **Источник в A:** `aa1f44383` (F2.30fix): `Internal/Runtime/Scopes/Services/LoadedScope.cs`, `Catalogues/Shared/AssetGroupBuilderExtensions.cs`, `Scopes/Common/Events/EventLoop.cs`, комментарий в `client/Tools~/ContainerGenerator/Scope/EdgeResolver.cs`.
- **Проблема:** `Internal/Runtime/Scopes/Services/LoadedScope.cs:45` вызывает `InvokeBeforeDispose` (выгрузку asset groups) до `Terminate`. Слушатели разборки читают уже выгруженные каталоги. В A это давало "... is not loaded" при выходе из матча.
- **Шаги:**
  1. Добавить фазу `AfterDispose` в `EventLoop`. Файл в B отличается от A, поэтому переносить вручную: только новую фазу, без оптимизаций A.
  2. Перевести выгрузку групп на эту фазу.
  3. Вызвать ее в `LoadedScope.Dispose` после `Terminate`.
- **Готово:** выход из матча в меню и повторный вход проходят без ошибок про незагруженные ассеты.

## C2. UIStateMachine и EventLoop.RunCustom

- **Приоритет:** P1. **Волна:** 4.
- **Источник в A:** `bfdbf4ce2` (F2.29): `Global/UI/StateMachines/UIStateMachine.cs`, `Scopes/Common/Events/EventLoop.cs`. В B нет `Internal/Runtime/Common/StateMachines/StateMachine.cs`, поэтому эта часть фикса A к B неприменима.
- **Проблема:**
  - `UIStateMachine` не удаляет хэндлы из `_handles` по окончании их lifetime: словарь растет, повторно открытое окно затирает хэндл.
  - `RunCustom` не передает lifetime.
- **Готово:** многократное открытие и закрытие окна не увеличивает `_handles`; вызывающий `RunCustom` код поправлен.

## C3. Fail-fast в ядре, Global и Meta

- **Приоритет:** P1. **Волна:** 4.
- **Источник в A:** `bfdbf4ce2` (F2.27, F2.28): `SceneServicesFactory.cs`, `Updater.cs`, `Global/Inputs/InputConstraintsStorage.cs`, `Global/Publisher/Itch/ItchSaves.cs`, `client/Assets/Meta/Connection/BackendProjectionHub.cs` (в B совпадает с A), неиспользуемые настройки в `Global/Settings/*`.
- **Проблема:** тихие пропуски вместо падения:
  - объект сцены, который не `ISceneService`, пропускается через `continue`;
  - отрицательная скорость `Updater` дает `return`;
  - несбалансированное снятие ограничения ввода зажимается в 0;
  - битый сейв в `ItchSaves` проглатывается;
  - `BackendProjectionHub` молча пропускает неизвестные проекции.
- **Шаги:**
  1. Наложить патч на совпадающие файлы.
  2. Прогнать Play Mode: меню, матч, выход. Поймать места, где B опирается на старое поведение; починить их или вынести вопросом оркестратору.
- **Готово:** компиляция, Play Mode без новых исключений, найденные нарушения записаны в `C3/handoff.md`.

## C4. Кодоген: примитивы по атрибуту, имена типов, Regenerate All

- **Приоритет:** P2. **Волна:** 5.
- **Источник в A:** `5288723cf` (F4.8), полный список файлов в `git show --stat 5288723cf`.
  - Генератор: `GraphWalker.cs` и `PrimitiveKind.cs`; `ContainerPrimitiveAttribute.cs` и атрибуты в `BuilderExtensions.cs` / `ScopeBuilderExtensions.cs`. Затем пересборка `client/Assets/Plugins/ContainerGenerator/ContainerGenerator.dll`.
  - Каталоги: `ScriptTypeNames.cs` и правки генераторов каталогов, `ContainerGraphAssetScanner.cs`, `RegenerateAll.cs`.
  - Связанный коммит `c9b4dd68d`: нормализация литералов цветов в сгенерированном `Colors.cs` под CoreCLR. Применять, только если B на CoreCLR-редакторе.
- **Проблема:**
  - Примитивы контейнера распознаются по строковым именам.
  - Тип берется только через `MonoScript.GetClass()`, поэтому при переименовании namespace кодоген попадает в дедлок.
  - Нет единой команды "Regenerate All".
- **Шаги:** наложить на совпадающие файлы; `SpriteGenerator`, `AudioCatalogGenerator`, `BuilderExtensions` в B отличаются, их править вручную. Порядок Regenerate All составить под набор генераторов B.
- **Готово:**
  - `ContainerGenerator.Verify` проходит;
  - Regenerate All на чистом дереве не меняет сгенерированные файлы;
  - сценарий "переименовать namespace сервиса -> Regenerate All -> компиляция" проходит без ручных правок.

## C5. Internal.Editor без ссылки на Flow

- **Приоритет:** P2. **Волна:** 5.
- **Источник в A:** `6d070bee4` (F0.1): `MockSwitcher` переносится в `Common/Flow/Editor/Flow.Editor.asmdef`, ссылка `Internal.Editor -> Flow` удаляется.
- **Проблема:** `Internal/Editor/Internal.Editor.asmdef` ссылается на Flow, ядро зависит от игрового потока. `MockSwitcher.cs` в B отличается (34 строки).
- **Готово:** в `Internal.Editor.asmdef` нет ссылок на игровые сборки; переключатель моков работает.

## C6. Мертвый код

- **Приоритет:** P3. **Волна:** 5.
- **Источник в A:** `429a82088` (F0.3, 65 файлов, из них 44 совпадают с B). Сразу учесть `8870e870d`: в A часть удаленного пришлось вернуть (UI state machine и HTTP backend stack), потому что они оказались живыми.
- **Шаги:**
  1. Каждый кандидат из `git show --stat 429a82088` проверить `grep` по B: используется ли он.
  2. Удалять только неиспользуемое. Список удаленного и оставленного записать в handoff.
- **Готово:** компиляция, Play Mode работает.

## C7. Контейнер инжектит только один экземпляр `builder.Inject` на тип

- **Приоритет:** P1. **Волна:** 6, вместе с C4: оба меняют генератор, dll пересобирается один раз.
- **Источник:** не A, а `/projects/storytale-workshop`, фикс от 2026-10-20 (на момент записи не закоммичен). Патч: `C7/inject_all_instances.patch`. В A тот же баг, там это задача F2.44.
- **Проблема:** сгенерированный скоуп получает экземпляр из `builder.Inject(x)` дыркой `request.Get<T>()`. `ContainerBuilder.TryGetHole` отдаёт последний добавленный экземпляр типа. Если в сцене несколько одинаковых сервисов (`ISceneService` с `builder.Inject(this)` в `Create`), `[Inject]` вызывается только у последнего из `SceneServicesFactory._services`. У остальных зависимости остаются `null`, и ошибка вылезает позже: в storytale-workshop это были тысячи `NullReferenceException` в `Update` второй кнопки. Ошибки сборки нет.
- **Фикс:**
  - Рантайм: `ContainerBuilder.GetInjections<T>()` возвращает все экземпляры `builder.Inject` этого типа. `GeneratedScopeRequest.GetInjections<T>()` бросает исключение, если экземпляров нет.
  - Генератор: в `ScopePlan.AssignHoles` для регистраций с `Kind == "Inject"` добавляется параметр конструктора `T[]` (`CtorParam.IsInjections`, `ElementType`), который `WriteRegister` заполняет `request.GetInjections<T>()`. `ScopeEmitter.WriteSlotConstruction` вызывает `[Inject]` в цикле по массиву. Поле слота по-прежнему держит последний экземпляр для зависимых регистраций. `HoleParam` и `HasCtorParam` такие параметры пропускают.
  - `RegisterComponent(this)` (тоже origin `InjectExisting`, но в `_injections` не попадает) не трогается.
- **Шаги:**
  1. Шаг 0: найти в сценах B два и больше экземпляров одного типа, который делает `builder.Inject(this)`. Можно в Play проверить, что у одного из них поле `[Inject]` пустое. Если дублей нет, баг всё равно латентный: фикс переносится, в handoff записать «дублей не найдено».
  2. `ScopePlan.cs` и `ScopeEmitter.cs` в B совпадают с storytale-workshop до фикса, их хунки накладываются патчем: `git apply --3way --include='client/Tools~/*' C7/inject_all_instances.patch`.
  3. `ContainerBuilder.cs` в B отличается (патч не накладывается), `GeneratedScopes.cs` накладывается со сдвигом. `GetInjections<T>()` перенести вручную рядом с `TryGetHole`.
  4. Комментарии в патче на русском — перевести на английский по правилам B.
  5. `dotnet build client/Tools~/ContainerGenerator/ContainerGenerator.csproj -c Release` (копирует dll в `client/Assets/Plugins/ContainerGenerator/`), затем компиляция в редакторе.
- **Проверка:** собрать сборку со сценой-скоупом с `-p:EmitCompilerGeneratedFiles=true` и убедиться, что для `builder.Inject` вызов идёт в цикле `xInjections[i].Inject(...)`. В Play у всех дублей заполнены `[Inject]`-поля. `ContainerGenerator.Verify` проходит.
- **Готово:** каждый экземпляр `builder.Inject` получает `[Inject]`. Play Mode (меню, матч, выход) проходит без новых ошибок.
