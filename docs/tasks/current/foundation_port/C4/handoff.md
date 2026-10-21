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
