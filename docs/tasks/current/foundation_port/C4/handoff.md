# T1 + C4 + C7: handoff

Исполнитель одной сессии, worktree `agent-a5aa481a75db1471f`, база `main` 77bb0ce5. Правки не закоммичены, новые файлы добавлены через `git add -N`. Редактор и Aspire не трогал. Файлы из `dirty_main.txt` не трогал.

## Шаг 0

- **T1.** Подтверждено. `.gitignore:84` (`/client/**/*.csproj`) скрывает csproj генератора и Verify: в основном дереве они есть, в git их нет, поэтому в worktree генератор не собирался. `.github/` в B нет.
- **C4.** Подтверждено чтением. `GraphWalker.IsPrimitiveBuilder` узнаёт примитивы по имени метода и имени класса. Каталоги и биндинги берут тип через `GetType()` / `MonoScript.GetClass()`. Единой команды перегенерации нет. Генераторы B те же, что в A на старте этапа: цвета, сцены, спрайты, аудио, биндинги, env-ассеты, префабы, asset map, shared addressables.
- **C4, `c9b4dd68d`.** Применим. `ProjectVersion.txt` = `7000.0.0a10` (CoreCLR). `ColorGenerator.FormatFloat` пишет `ToString("R")`. На Mono это 9 знаков (`0.458823532f`), на CoreCLR это кратчайшая запись (`0.45882353f`). Значит, первый запуск генератора в новом редакторе перепишет `Colors.cs`. В текущем `Colors.cs` B 17 строк в старом формате.
- **C7.** Баг латентный, дублей нет. В коде B (кроме генератора и рантайма контейнера) ровно один вызов `builder.Inject`: `GlobalUIExtensions.AddLoadingScreen` → `builder.Inject(loadingScreen)`. Это один экземпляр из `builder.Instantiate(GlobalPrefabs.LoadingScreen)`. По YAML скрипт `LoadingScreen` (guid `2c2c9ef2...`) встречается только в `LoadingScreen.prefab`, один раз. Сервисы сцен B регистрируются через `builder.RegisterComponent(this)`, а не через `builder.Inject(this)`. Поэтому сцены и префабы с дублями `builder.Inject(this)` не найдены. Фикс перенесён как защита.

## Что перенесено

### T1 (A `b9f89f20b`)

- `.gitignore`: исключение `!/client/Tools~/**/*.csproj`, игнор `bin/`, `obj/` в `client/Tools~`. Вставлено вручную, контекст A не совпал.
- `client/Tools~/ContainerGenerator/ContainerGenerator.csproj` побайтно совпадает с файлом основного дерева B.
- `client/Tools~/ContainerGenerator.Verify/ContainerGenerator.Verify.csproj` взят из A: `net10.0` вместо `net9.0` в основном дереве B. В CI ставится только SDK 10.
- README генератора из A (B совпадал с A до коммита) плюс абзац про `GetInjections` (C7).
- `.github/workflows/ci.yml` из A без правок. Пути B те же: `backend/backend.slnx`, `backend/Tools/Tests/bin/Debug/net10.0/Tests`, `client/Tools~`, `.claude/docs/TESTING.md`. Backend ничего не берёт из `client/`. CI на GitHub не запускал, пуш только с согласия пользователя.

### C4 (A `5288723cf`, `c9b4dd68d`)

- Генератор: `Graph/PrimitiveKind.cs` (новый), `GraphWalker.cs` и Verify (2 теста A) — патчем.
- Рантайм: `ContainerPrimitiveAttribute.cs` (новый, guid из A). `[ContainerPrimitive]` на `ScopeBuilderExtensions` наложен патчем. `BuilderExtensions.cs` размечен вручную: в B методы возвращают `IServiceRegistration`, у A `ServiceRegistration`. Русские комментарии файла переведены.
- Редактор: `ScriptTypeNames.cs`, `Tools/Codegen/RegenerateAll.cs` (новые), `GenerateNow()` у всех генераторов, `CatalogGenerationRunner.RunNow()`, `ContainerGraphAssetScanner` (чтение из исходников, хунк с комментарием вручную), `HierarchyBindingsBatch.PrepareProject`, `HierarchyBindingsGenerator.Prepare(..., bindUnchanged)` (хунк вручную), `HierarchyBindingsScanner`, `AssetCatalogGenerator`, `PrefabCatalogGenerator`. `SpriteGenerator`, `AudioCatalogGenerator` и `SharedAddressablesSync` в B отличаются от A только комментариями, хунки `GenerateNow` наложились с `-C1`.
- `Colors.cs`: 17 строк приведены к кратчайшей записи float (round-trip через float32 проверен скриптом). Значения цветов не изменились.
- Документация: `docs/obsidian/architecture/client-dev-tools.md` (раздел "Regenerate All", примитивы по атрибуту, Verify, `GetInjections`), `client-common.md` (строка `BuilderExtensions`). `.claude/docs/CODEGEN.md` и `HIERARCHY_BINDINGS.md` из A в B не существуют, их суть перенесена в obsidian.

### C7 (патч storytale-workshop)

- `ScopePlan.cs`, `ScopeEmitter.cs`: хунки наложились чисто (`--3way --include='client/Tools~/*'`). Комментарии переведены.
- `ContainerBuilder.GetInjections<T>()` (вручную, рядом с `TryGetHole`) и `GeneratedScopeRequest.GetInjections<T>()` (вручную). Бросает исключение, если экземпляров нет.
- В заглушку `GeneratedScopeRequest` в Verify добавлен `GetInjections<T>()`, как в storytale.
- Новый Verify-тест `builder.Inject calls [Inject] on every instance`. На коде без C7 падает: вызывается `request.Get<T>()`, цикла нет.

## Чего нет

- `.claude/docs/CODEGEN.md`, `HIERARCHY_BINDINGS.md`, правка `COMMON_CONTAINER.md` из A: в B таких файлов или раздела нет (см. выше).
- Хунк A с `AssetDatabase.IsAssetImportWorkerProcess()` в `CatalogGenerationRunner.Run()`: это другой коммит A, не C4. `RunNow()` эту проверку содержит.

## Отличия от A

- Меню `Tools/Regenerate All` вместо `Tools/Foundation/Regenerate All`: "Foundation" — название этапа A, в B оно ничего не значит. Остальные генераторы B тоже висят прямо в `Tools/`.
- `BuilderExtensions` с `IServiceRegistration` (см. выше).
- Verify csproj: `net10.0`, как в A (в основном дереве B был `net9.0`).

## Отличия от storytale-workshop (C7)

- **Добавлено:** у одного типа может быть несколько вызовов `builder.Inject`. Патч storytale в этом случае дал бы каждому слоту свой массив `GetInjections<T>()`, а этот массив уже содержит экземпляры всех вызовов. Тогда `[Inject]` вызывался бы дважды у каждого экземпляра. Вот как это решено в B. В `ScopePlan` первый слот типа владеет массивом и циклом. Остальные слоты того же типа помечены `IsInjectionsRepeat`, и `ScopeEmitter` для них ничего не пишет. Новый Verify-тест покрывает именно этот случай (два вызова `builder.Inject(new SceneButton())`). До C7 в этом случае оба слота получали последний экземпляр.
- Остаётся крайний случай: `builder.Inject` с базовым типом и с наследником в одном скоупе. `GetInjections<Base>()` вернёт и экземпляры наследника (`is T`, как в `TryGetHole`), и они получат `[Inject]` дважды. В B такого кода нет.
- `GeneratedScopeRequest` в B — `sealed class` (в storytale — `readonly struct`). На перенос это не влияет.
- В `ContainerBuilder` B метод `ThrowIfBuilt` приватный. `GetInjections` его вызывает: фабрика скоупа выполняется до `MarkBuilt()`.

## Наблюдение (вне задачи)

`GlobalUIExtensions.AddLoadingScreen` делает и `builder.Inject(loadingScreen)`, и `builder.RegisterInstance(loadingScreen)`. Сгенерированный скоуп вызывает `LoadingScreen.Construct(_updater)` дважды: один раз через Inject и один раз через RegisterInstance. Так было и до C7. Это безвредно (метод только присваивает поле), но `builder.Inject` там лишний.

## Проверка

- Генератор: `dotnet build client/Tools~/ContainerGenerator/ContainerGenerator.csproj -c Release` — 0 ошибок. Одно предупреждение CS8602 в `ScopePlan.cs:168` было и раньше. Verify (`dotnet run ... -c Release`): `ALL PASSED`, 3 новых теста (2 из A, 1 на C7).
- Клиент: `*.csproj`/`client.sln` временно скопированы из основного дерева в `client/` worktree. Пути `Packages/` направлены в основное дерево. Analyzer `ContainerGenerator.dll` взят из worktree. Новые .cs добавлены в копии `Internal`/`Internal.Editor`. Из копий выкинуты файлы, которых нет в worktree (незакоммиченный `WebGlDeploy.cs` и устаревшие проекты). Собраны `Flow` (тянет все runtime-сборки), `GamePlay.Editor`, `Global.Editor`, `Internal.Editor`, `Internal.Tests`: 0 ошибок, ни одного CINGR. Копии, `client/obj` и `client/Temp` удалены.
- Сгенерированный код (`Global` с `-p:EmitCompilerGeneratedFiles=true`): в `GlobalScopeExtensionsConstructContainer` стоят параметр `LoadingScreen[] loadingScreenInjections` и аргумент `request.GetInjections<LoadingScreen>()`, `Construct` вызывается в цикле `for (... loadingScreenInjections.Length ...)`.
- Backend (команды CI): `dotnet build backend/backend.slnx` — 0 ошибок; полный прогон `backend/Tools/Tests/bin/Debug/net10.0/Tests` (Docker есть) — 1890/1890, пропущенных нет.

## Как пересобрать dll

```bash
dotnet build client/Tools~/ContainerGenerator/ContainerGenerator.csproj -c Release
dotnet run --project client/Tools~/ContainerGenerator.Verify/ContainerGenerator.Verify.csproj -c Release
```

Таргет `CopyToUnityPlugins` копирует dll в `client/Assets/Plugins/ContainerGenerator/` после **любой** сборки. Debug-сборка Verify тоже перезапишет плагин Debug-версией. Последняя сборка должна быть Release. `client/Assets/Plugins/ContainerGenerator/ContainerGenerator.dll` в worktree совпадает с `bin/Release/netstandard2.0/ContainerGenerator.dll` (146944 байт). В основном дереве собирать только при переносе: редактор подхватит dll на следующей компиляции.

## Что проверить в редакторе

1. Компиляция без ошибок и без CINGR00* (Unity сама создаст `.meta` только для того, чего нет; `.meta` новых файлов взяты из A).
2. `Tools/Regenerate All` на чистом дереве: в логе шаги `[RegenerateAll] Colors ... Shared addressables` и `Done`. После этого `git status` не должен показать изменений. Если `Colors.cs` изменился, значит, нормализация `c9b4dd68d` не совпала с выводом редактора; закоммитить то, что выдал редактор. Незагруженные сцены с биндингами попадут в лог, это нормально.
3. Сценарий из C4: переименовать namespace сервиса с bindings или catalog-ссылкой → компиляция падает → `Tools/Regenerate All` (из старого домена, в логе будет предупреждение) → компиляция проходит без ручных правок. Откатить.
4. Сцены с дублями `builder.Inject`: в B их нет (шаг 0). Достаточно проверить, что экран загрузки работает (единственный `builder.Inject`).
5. Play: меню → матч с ботом → выход, без новых ошибок в консоли.

## Изменённые файлы

- `.gitignore`, `.github/workflows/ci.yml` (новый)
- `client/Tools~/ContainerGenerator/ContainerGenerator.csproj` (новый в git), `client/Tools~/ContainerGenerator.Verify/ContainerGenerator.Verify.csproj` (новый в git), `client/Tools~/ContainerGenerator/README.md`, `Graph/GraphWalker.cs`, `Graph/PrimitiveKind.cs` (новый), `Scope/ScopePlan.cs`, `Scope/ScopeEmitter.cs`, `client/Tools~/ContainerGenerator.Verify/Program.cs`
- `client/Assets/Plugins/ContainerGenerator/ContainerGenerator.dll`
- `client/Assets/Common/Internal/Runtime/`: `Scopes/Common/Builders/BuilderExtensions.cs`, `Scopes/Services/Builder/ScopeBuilderExtensions.cs`, `Tools/Container/Generated/ContainerPrimitiveAttribute.cs(.meta)` (новый), `Tools/Container/Runtime/ContainerBuilder.cs`, `Tools/Container/Runtime/GeneratedScopes.cs`, `Catalogues/Colors/Generated/Colors.cs`
- `client/Assets/Common/Internal/Editor/`: `Catalogues/Assets/AssetCatalogGenerator.cs`, `Catalogues/Audio/AudioCatalogGenerator.cs`, `Catalogues/Colors/ColorGenerator.cs`, `Catalogues/Prefabs/PrefabCatalogGenerator.cs`, `Catalogues/Shared/CatalogGenerationRunner.cs`, `Catalogues/Shared/ScriptTypeNames.cs(.meta)` (новый), `Catalogues/Shared/SharedAddressablesSync.cs`, `Catalogues/Sprites/SpriteGenerator.cs`, `Tools/Codegen.meta`, `Tools/Codegen/RegenerateAll.cs(.meta)` (новые), `Tools/ContainerCodegen/ContainerGraphAssetScanner.cs`, `Tools/ContainerCodegen/ContainerGraphPostprocessor.cs`, `Tools/HierarchyBindings/HierarchyBindingsBatch.cs`, `HierarchyBindingsGenerator.cs`, `HierarchyBindingsScanner.cs`
- `docs/obsidian/architecture/client-common.md`, `client-dev-tools.md`
- Этот файл.

Коммит: `(port of peace-data-reborn b9f89f20b, 5288723cf, c9b4dd68d)` + C7 из storytale-workshop.

## Доработка: дрейф HierarchyBindings

База: `main` 750de794 (ff). Правки не закоммичены. Редактор и Aspire не трогал. Сгенерированные `.g.cs` и префабы руками не правил.

### Причины по пунктам

1. **`GameCardBindings.g.cs` и `Game_Card.prefab` (MeshFilter, 3 прохода).** Причина в баге сканера. Он старый, это не регрессия C4. Unity 7000 компоненты TMP тоже не менял. `TextMeshPro.Awake` (и геттер `meshFilter`) добавляет `MeshFilter` с `HideFlags.HideAndDontSave` (`Packages/com.unity.ugui/Runtime/TMP/TextMeshPro.cs:170, 564-577`). Такой компонент никогда не сохраняется, а ссылка на него сериализуется как `{fileID: 0}`. Есть ли он на объекте, зависит от того, как объект загружен:
   - `PrepareProject` сканирует ассет из `LoadAssetAtPath`. `Awake` там не вызывается, поэтому `MeshFilter` есть только у тех объектов, где его геттер кто-то уже дёрнул. Так у `ManaCost` поле осталось, а у `Name` и `Description` пропало.
   - `HierarchyBindingsBinder` сканирует `LoadPrefabContents`. Там `Awake` отработал, поэтому `MeshFilter` есть у всех трёх объектов.
   
   Отсюда первый проход: код без поля, связывание видит компонент, и падает `'_meshFilter' is missing. The code is not compiled yet` (сообщение ложное, код скомпилирован). Префаб не сохраняется, хэш остаётся старым. На следующих проходах код не меняется, поэтому из-за `bindUnchanged = false` (C4) связывание больше не запускается. Префаб пересохраняется косвенно, скорее всего через `SaveAssets`: TMP пометил ассет dirty своим `AddComponent`. При этом Unity выкидывает неизвестные `_meshFilter: {fileID: 0}`, а `_structureHash` остаётся `1f3189…`. Тот же баг объясняет коммит `1b65034c` (апгрейд на Unity 7000): тогда код генерировали с открытым содержимым префаба, и `MeshFilter` попал в код у всех трёх объектов с `{fileID: 0}` в префабе. Сам апгрейд к этому отношения не имеет.
2. **`GameOwnResourceRowBindings.g.cs` (корень `Health`).** Старый рассинхрон из `5a671a1e`. Код сгенерирован с экземпляра префаба `Health` в `GamePlay/Assets/Scenes/Game_Overlay.unity`, а не из ассета. Хэш кода `73aedc…` совпадает с override `_structureHash` этого экземпляра в сцене. В самом префабе лежит `ec9394…`. Каждый экземпляр (их 6 с override хэша) генерировал свой хэш, в файл попал последний. Генератор такое пропускал: `TryDescribe` считал экземпляр объектом сцены и писал хэш и ссылки как scene overrides. Это баг генератора, исправлен (см. ниже). Regenerate All берёт владельца из ассета, поэтому корень стал `Game_Own_Resource_Row`.
3. **`GameUIResultsBindings.g.cs` (+53 строки дерева).** Старый рассинхрон. Файл сгенерирован в `3b47f550` (2026-09-05 02:55) и с тех пор не перегенерировался. Дерево в комментарии (`AppendHierarchy`) добавлено позже, в `9c51d9eb` (04:02 того же дня). Формат хэша поменялся в `5a671a1e` (`DeclaredTypeName`, элементы массивов). Отсюда новый хэш. C4 тут ни при чём.
4. **`Game_UI_Results.prefab` (удалены `_stats`, `_menuButton`, `_rematchButton`, `_gameResultsStats`).** Следствие п. 3. Этих полей нет ни в `GameResultsView`, ни в биндингах с `3b47f550`: старые `[SerializeField]`, которые остались в YAML. Связывание пересохраняет префаб через `SaveAsPrefabAsset`, и Unity выкидывает неизвестные поля. Это ожидаемая разовая чистка.

Регрессия C4 только одна: из-за `bindUnchanged = false` объект, у которого связывание однажды не прошло, больше никогда не догонял свой код (п. 1). До C4 связывание шло всегда.

### Правка

- `HierarchyBindingsScanner.CollectComponents`: пропускаются компоненты с `HideFlags.DontSaveInEditor`. Ассет и содержимое префаба теперь дают одну структуру.
- `HierarchyBindingsRequest.StructureHash` и `HierarchyBindingsBinder.Bind`: связывание сверяет хэш повторного скана с хэшем, из которого сгенерирован код. При расхождении выдаётся ошибка с обоими хэшами, а не ложное «not compiled yet». В префаб записывается проверенный хэш.
- `HierarchyBindingsGenerator.Prepare`:
  - связывание ставится в очередь и тогда, когда код не изменился, но `_structureHash` на объекте устарел (`isBoundStale`). Так Regenerate All сходится за один проход и чинит объекты, у которых связывание раньше упало. На чистом дереве хэши совпадают, и префабы не пересохраняются;
  - fail fast, если `ObjectBindings` на объекте пришёл из префаба-источника (экземпляр в сцене, вариант, вложенный префаб): ошибка с путём ассета. Кнопка Regenerate на экземпляре больше не пишет scene overrides.
- `docs/obsidian/architecture/client-dev-tools.md`: правила сканера и шаг 2 Regenerate All.

Сборка: `Internal.Editor.csproj` по временным копиям csproj из основного дерева. Пути `Packages/` направлены в основное дерево, анализатор взят из worktree. В копии добавлены три новых файла C4, которых нет в csproj основного дерева: `ContainerPrimitiveAttribute.cs`, `ScriptTypeNames.cs`, `RegenerateAll.cs`. Без них сборка даёт ложные CINGR001. Результат: 0 ошибок, в HierarchyBindings нет предупреждений. Копии, `client/obj` и `client/Temp` удалены.

### Что ожидается после перегенерации (Tools/Regenerate All на чистом дереве)

- Первый проход, затем перекомпиляция и связывание. В логе `Bound N of N bindings` без ошибок, `'_meshFilter' is missing` больше нет.
  - `GameCardBindings.g.cs`: поля `MeshFilter` пропадают у **всех трёх** TMP (`Name`, `Description`, `ManaCost`). Хэш новый: не `1f3189…` и, скорее всего, не `451397…` (тот был с `MeshFilter` у `ManaCost`). `Game_Card.prefab`: три строки `_meshFilter: {fileID: 0}` удалены, `_structureHash` совпадает с хэшем в `.g.cs`.
  - `GameOwnResourceRowBindings.g.cs`: корень `Game_Own_Resource_Row`, новый хэш. `Game_Own_Resource_Row.prefab`: `_structureHash` = хэш кода (сейчас там `ec9394…`).
  - `GameUIResultsBindings.g.cs`: дерево в комментарии и новый хэш. `Game_UI_Results.prefab`: удалены `_stats`, `_menuButton`, `_rematchButton`, `_gameResultsStats`, `_structureHash` = хэш кода.
- Проверить: в каждом изменённом префабе `_structureHash` совпадает со строкой `// Structure:` его `.g.cs`.
- Второй прогон Regenerate All: `git status` пуст (сходимость за один проход). Если нет, в логе будет ошибка `Structure of '…' is X, the code was generated from Y` с путём объекта. Это следующий источник недетерминизма скана, прислать лог.
- Кнопка Regenerate на экземпляре `Health` в `Game_Overlay` даёт ошибку `carries bindings of prefab Assets/GamePlay/Assets/Prefabs/UI/Game_Own_Resource_Row.prefab`.
- Компиляция без ошибок: `MeshFilter` из биндингов карты нигде не используется (grep).

### Остаток (не трогал)

`Game_Overlay.unity` хранит 6 устаревших override `_structureHash` у экземпляров ресурсных строк и override `_largeRow._designContainer`. В рантайме хэш никто не читает (только Binder пишет). Regenerate All сцены не трогает, поэтому на чистоту `git status` это не влияет. При желании убрать в редакторе: Overrides → Revert на этих полях, сцену открыть и сохранить вручную.

Изменённые файлы: `client/Assets/Common/Internal/Editor/Tools/HierarchyBindings/HierarchyBindingsScanner.cs`, `HierarchyBindingsBinder.cs`, `HierarchyBindingsGenerator.cs`, `HierarchyBindingsRequest.cs`, `docs/obsidian/architecture/client-dev-tools.md`, этот файл.
