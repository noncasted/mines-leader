# Клиент: размер WebGL-билда

Как уменьшали WebGL-билд, какие правки в проекте и пакетах за этим стоят и что повторить при обновлении Unity.

## Главный принцип

Линкер Unity подключает модуль движка целиком, если в финальных сборках осталась **хотя бы одна** ссылка на его типы. Вместе с модулем в билд попадают его нативные зависимости. Например, одна ссылка URP на `UIElementsRuntimeUtility` держала в билде 1594 из 2244 типов `UnityEngine.UIElementsModule`, а один вызов буфера обмена в `InputField` держал модули IMGUI и TextCoreTextEngine.

Часть таких ссылок в пакетах Unity включается через `versionDefines` в asmdef: define появляется сам, если в manifest есть модуль. Модули нельзя убрать из manifest, потому что от них зависят редакторные пакеты (MCP for Unity, Aseprite Importer). Поэтому сами пакеты **встроены** в `client/Packages/`, а правки сделаны в их копиях.

## Результат (br, `client/build`)

Сентябрь 2026, Unity 6000.x, сумма файлов плеера:

| Шаг | Размер | Разница |
|---|---|---|
| Исходно (после Newtonsoft → JsonUtility и стриппинга) | 8 020 KB | — |
| Удалён Input System | 7 378 KB | −642 KB |
| UGUI без `PACKAGE_UITOOLKIT` | 7 340 KB | −38 KB |
| URP без `ENABLE_UIELEMENTS_MODULE` | 6 454 KB | −885 KB |
| Удалён модуль XR | 6 366 KB | −88 KB |
| Physics и Animation | 6 323 KB | −43 KB |
| `ClientWebSocket` скрыт из WebGL, шейдеры Built-in RP отключены | 6 000 KB | −323 KB |

21 октября 2026, Unity 7000.0.0a10, `wasm.br` + `data.br`:

| Шаг | Размер | Разница |
|---|---|---|
| После обновления до 7000, на URP | ~5 780 KB | — |
| Свой 2D SRP вместо URP | ~5 300 KB | −483 KB |
| Без 2D-физики: свой hit-test, Tilemap отфильтрован | ~5 010 KB | −296 KB |
| 106 сабмодулей вырезаны по профилю | ~4 600 KB | −426 KB |
| Урезанный MemoryPack.Core | ~4 200 KB | −396 KB |
| `link.xml` Addressables без `preserve="all"` на типах из бандлов | ~4 110 KB | −88 KB |
| Без модуля IMGUI | ~3 910 KB | −177 KB |

Итог: `wasm.br` 2.85 MB, `data.br` 1.06 MB, `framework.js.br` 64 KB, `loader.js` 28 KB.

Для сравнения: пустой проект Unity 6 со всеми настройками размера весит около 2 MB br. Остаток — наш набор фич: TMP и UGUI, игровой код и протокол, Addressables и метаданные il2cpp (`global-metadata.dat`, около 780 KB br).

## Форки пакетов

Форки Unity-пакетов лежат во фреймворке: `tools/framework/client/packages/<пакет>` (git submodule), `manifest.json` ссылается на них через `file:`. В каждом пакете есть `FORK.md` со списком правок; история в git фреймворка: сначала коммит с оригиналом, затем по коммиту на правку.

| Пакет | Правка |
|---|---|
| `com.unity.ugui` | `Runtime/UGUI/UnityEngine.UI.asmdef`: удалены versionDefines `PACKAGE_UITOOLKIT`, `PACKAGE_PHYSICS`, `PACKAGE_PHYSICS2D`, `PACKAGE_ANIMATION`. В ugui 7.0.0 `InputField` и `TMP_InputField` уже работают с буфером обмена через `SystemCopyBuffer` (без IMGUI) |
| `com.unity.render-pipelines.core` | `Runtime/Unity.RenderPipelines.Core.Runtime.asmdef` и `Editor/Unity.RenderPipelines.Core.Editor.asmdef`: удалён `ENABLE_PHYSICS_MODULE` |
| `com.unity.addressables` | `Editor/.../SchemaBuilders/BundledAssetSchemaBuilder.cs`, `AddIncludedTypes`: типы-наследники `UnityEngine.Object` из бандлов пишутся в `link.xml` как `preserve="nothing" serialized="true"`, а не `preserve="all"` |
| `com.unity.web.stripping-tool` | `Editor/Core/WasmOpt.cs`: добавлен `--enable-exception-handling`. `StrippingProjectSettings.cs`: повторная загрузка настроек через `delayCall` |

Пакеты скопированы без `Documentation~`, `Samples~` и `Tests`.

> [!danger] Web Stripping Tool без `--enable-exception-handling`
> Пакет после билда прогоняет wasm через `wasm-opt -O3`, но не включает фичу exceptions. Binaryen тогда считает, что вызовы не бросают, и выкидывает все `try`/`catch`: в релизном wasm их было 0 при ~9000 после линковки. Любое исключение, даже пойманное в C#, роняет плеер (`Uncaught exception from main loop`). Проверка после обновления пакета: `wasm-dis --all-features <build>.wasm | grep -c '(try'` должно быть больше нуля.

> [!danger] Сборки Addressables сохраняются целиком
> `m_Linker.AddAssemblies(Addressables, ResourceManager)` в `BundledAssetSchemaBuilder` убирать нельзя. ResourceManager создаёт операции (`GroupOperation`) и адаптеры сериализации каталога через рефлексию. Без полного сохранения в плеере падает `MissingMethodException: Default constructor not found ... GroupOperation`, и `catalog.bin` не загружается.

Что теряем:
- **UGUI:** в билде не работают `PhysicsRaycaster` и `Physics2DRaycaster`, Animation-переходы `Selectable` и связка EventSystem с панелями UI Toolkit.
- **IMGUI:** `OnGUI` не вызывается, `Event.current` равен null. Новое использование `GUI`, `GUILayout` или `GUIUtility` молча вернёт модули IMGUI и TextCoreTextEngine в билд: сборка не упадёт, просто вырастет размер.
- **RP Core:** не работают локальные Volume с коллайдерами.

## Урезанный MemoryPack.Core

Клиент использует пакет фреймворка `com.cysharp.memorypack` (`tools/framework/client/packages/com.cysharp.memorypack`): DLL, собранная из исходников MemoryPack 1.21.4 без лишних форматтеров, вместо NuGet-версии. Подробности и порядок обновления описаны в его `FORK.md`.

NuGet-версия помечает почти все форматтеры `[Preserve]` и регистрирует их в статическом конструкторе, поэтому в билд попадали System.Collections.Immutable, нативный Brotli, System.Numerics, Uri, TimeZoneInfo, CultureInfo, Regex и кортежи. Исходники нельзя положить в Unity напрямую: они требуют C# 11, а Unity компилирует на C# 9.

Бэкенд использует полный пакет из NuGet, формат на проводе тот же. Проверка: EditMode-тест `Internal.Tests.ProtocolBytesTests`.

## Остальные правки

- **Свой 2D SRP вместо URP.** Пайплайн лежит в `Assets/Common/Rendering`, ассет `Assets/Settings/GamePipeline.asset`.
- **2D-физика удалена.** Клики по картам и клеткам обрабатывает свой hit-test: `CardsPointer` (`GamePlay/Cards/Services`) проверяет прямоугольники `CardPointerArea`, сверху оказывается карта с большим SortingGroup. `PlayerAssemblyFilter` фреймворка (`IFilterBuildAssemblies`, список в `ProjectSettings/Framework.json`, `StrippedPlayerAssemblies`) выкидывает из билда `Unity.2D.Tilemap.Runtime`: его тянет Aseprite Importer через `[AlwaysLinkAssembly]`, а модуль Tilemap зависит от Physics2D. В UniTask удалён `UNITASK_PHYSICS2D_SUPPORT`.
- **Input System удалён.** Ввод идёт через старый `Input`. В `Global_Events.prefab` стоит `StandaloneInputModule`.
- **Модуль XR** (`com.unity.modules.xr`) удалён из manifest.
- **UniTask** (форк во фреймворке, `tools/framework/client/packages/com.cysharp.unitask`): удалены `UNITASK_PHYSICS_SUPPORT` и `UNITASK_PHYSICS2D_SUPPORT`.
- **`NetworkConnection.CreateWebSocket()`:** `DefaultWebSocket` (`ClientWebSocket`) скрыт под `#if UNITY_EDITOR || !UNITY_WEBGL`. Без этого в билд попадали `System.Net`, TLS, X509 и `Mono.Security`.
- **Web Stripping Tool:** настройки в `Assets/Settings/DefaultSubmoduleStrippingSettings.asset`, стриппинг запускается автоматически после билда. Список из 106 сабмодулей собран по отчёту профилирования (см. ниже). Часть записей (PhysX, Tilemap, HarfBuzz, ICU) сейчас ничего не вырезает: этих модулей и так нет в билде. Записи оставлены на случай, если модуль вернётся.

## Проверено и отклонено

- **`data.unity3d` без LZ4HC** (`BuildOptions.UncompressedAssetBundle`). Это единственный способ: на Web настройки сжатия в Build Profile и флаги `CompressWithLz4*` игнорируются. Выигрыш всего −47 KB `data.br`, а распакованные данные занимают на 1.5 MB больше в heap wasm.
- **Убрать UICommon.** Без `Event.PopEvent` не работает ввод с клавиатуры в `TMP_InputField`, а выигрыш около 3.5 KB.
- **Content Directories вместо Addressables.** Оценка выигрыша 50–100 KB br. ContentLoad, Regex и Uri остаются в билде в любом случае, а переход требует переписать загрузку и деплой контента.

## Ограничения

- **Пост-обработку не вырезать:** bloom и другие эффекты появятся позже, их надо встраивать в свой пайплайн.
- **Не возвращать `link.xml` с `preserve="all"`** на собственные сборки: он отключает стриппинг кода.
- **`WebGlBuild.cs` зависит от активного build profile** (`web_dev`). В нём Code Optimization стоит в `DiskSizeMaxLTO` (`-Oz` + LTO), и это максимум. Без активного профиля сборка возьмёт настройки из `ProjectSettings` и станет заметно больше.

## При обновлении Unity или пакетов

Форки обновляются во фреймворке (`tools/framework`), для всех проектов сразу:

1. Закоммитить во фреймворке чистую новую версию пакета (из `Library/PackageCache/<пакет>@<хеш>` или из `Editor/Data/Resources/PackageManager/BuiltInPackages` редактора) без `Documentation~`, `Samples~` и `Tests`. У Web Stripping Tool оставить `ProfilingTemplate~`: его читает код пакета.
2. Перенести поверх коммиты с правками (`git log -- client/packages/<пакет>`), обновить `FORK.md`. Проверить, не появились ли новые versionDefines на `com.unity.modules.uielements`, `physics`, `physics2d`, `animation` или `xr`.
3. Проверить, что путь к профилю в `ProjectSettings/Packages/com.unity.web.stripping-tool/Settings.json` не пустой и что в логе сборки есть «WASM Submodule Stripping».
4. Собрать билд и сравнить список модулей в `UnityClassRegistration.cpp` с прошлым.

> [!warning] Устаревший импорт asmdef
> Если править asmdef при открытом Unity сразу после копирования пакета, Unity может скомпилировать сборку со старым импортом, и define останется. Правьте при закрытом редакторе или переимпортируйте asmdef принудительно: `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)`.

## Как искать, что ещё лишнее

- **Причины подключения модулей:** `Library/Bee/artifacts/WebGL/ManagedStripped/wasm/UnityLinkerToEditorData.json`, раздел `report.modules[].dependencies`.
  - `dependencyType 0` — нативный класс используется кодом или ассетами;
  - `2` — модуль нужен другому модулю;
  - `3` — модуль обязателен всегда.
  - Пустой список значит, что модуль держит управляемый код.
- **Список модулей в билде:** `Library/Bee/artifacts/WebGL/il2cppOutput/UnityClassRegistration.cpp`, вызовы `RegisterModule_*`.
- **Какие сборки остались после стриппинга:** `Library/Bee/artifacts/WebGL/ManagedStripped/wasm/*.dll`. Кто ссылается на модуль, видно по `TypeReference` в этих сборках.
- **Нативные классы из сцен:** `Library/Bee/artifacts/UnityLinkerInputs/EditorToUnityLinkerData.json`, раздел `typesInScenes`.
- **Какие сабмодули реально вызываются.** Собрать билд без автоматического стриппинга и с `debugSymbolMode = Embedded`, затем вызвать `WebBuildProcessor.InstrumentBuild`. В игре на оверлее нажать «Download Report» и получить `submodule_report.json`. Сабмодули с `false` за сессию не вызывались. Перед переносом в список стоит прогнать билд с `MissingSubmoduleErrorHandling = LogError`: вызов вырезанной функции тогда пишет в консоль «Stripped function … was called».
- **Вес функций.** В wasm с отладочными символами есть секция `name`. Размеры тел функций по именам вместе с картой «метод → сборка» из `il2cppOutput/cpp/*.cpp` показывают, сколько весит каждая сборка или подсистема.
