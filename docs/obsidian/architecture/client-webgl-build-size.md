# Клиент: размер WebGL-билда

Как уменьшали WebGL-билд, какие правки в проекте и пакетах за этим стоят и что повторить при обновлении Unity.

## Главный принцип

Линкер Unity подключает модуль движка целиком, если в финальных сборках осталась **хотя бы одна** ссылка на его типы. Вместе с модулем в билд попадают его нативные зависимости. Например, одна ссылка URP на `UIElementsRuntimeUtility` держала в билде 1594 из 2244 типов `UnityEngine.UIElementsModule`. Через зависимости это ещё и Properties, IMGUI-зависимости, Physics и Animation.

Часть таких ссылок в пакетах Unity включается через `versionDefines` в asmdef: define появляется сам, если в manifest есть модуль. Модули нельзя убрать из manifest, потому что от них зависят редакторные пакеты (MCP for Unity, 2D Tilemap Editor). Поэтому сами пакеты **встроены** в `client/Packages/`, а define удалены из их asmdef.

## Результат (br, `client/build`)

| Шаг | Размер | Разница |
|---|---|---|
| Исходно (после Newtonsoft → JsonUtility и стриппинга) | 8 020 KB | — |
| Удалён Input System | 7 378 KB | −642 KB |
| UGUI без `PACKAGE_UITOOLKIT` | 7 340 KB | −38 KB |
| URP без `ENABLE_UIELEMENTS_MODULE` | 6 454 KB | −885 KB |
| Удалён модуль XR | 6 366 KB | −88 KB |
| Physics и Animation | 6 323 KB | −43 KB |
| `ClientWebSocket` скрыт из WebGL, шейдеры Built-in RP отключены | 6 000 KB | −323 KB |

## Встроенные пакеты

| Пакет | Путь | Правка |
|---|---|---|
| `com.unity.ugui` 2.7.0 | `Packages/com.unity.ugui` | `Runtime/UGUI/UnityEngine.UI.asmdef`: удалены versionDefines `PACKAGE_UITOOLKIT`, `PACKAGE_PHYSICS`, `PACKAGE_ANIMATION` |
| `com.unity.render-pipelines.universal` 17.7.0 | `Packages/com.unity.render-pipelines.universal` | `Runtime/Unity.RenderPipelines.Universal.Runtime.asmdef`: удалён `ENABLE_UIELEMENTS_MODULE` |
| `com.unity.render-pipelines.core` | `Packages/com.unity.render-pipelines.core` | `Runtime/Unity.RenderPipelines.Core.Runtime.asmdef` и `Editor/Unity.RenderPipelines.Core.Editor.asmdef`: удалён `ENABLE_PHYSICS_MODULE` |

Пакеты скопированы без `Documentation~`, `Samples~` и `Tests`. Код внутри не менялся, только asmdef.

Что теряем:
- **UGUI:** в билде не работают `PhysicsRaycaster`, блокировка лучей 3D-коллайдерами в `GraphicRaycaster`, Animation-переходы `Selectable` и связка EventSystem с панелями UI Toolkit.
- **URP:** нет композиции backdrop-фильтров overlay-панелей UI Toolkit.
- **RP Core:** не работают локальные Volume с коллайдерами. Глобальные Volume работают.

## Остальные правки

- **Input System удалён.** Ввод идёт через старый `Input`: `GameInput`, `MenuDecks`. В `Global_Events.prefab` стоит `StandaloneInputModule`. Active Input Handling переключён на Input Manager (Old).
- **Модуль XR** (`com.unity.modules.xr`) удалён из manifest. От него зависели define `ENABLE_XR_MODULE` в URP и RP Core.
- **UniTask** (`Assets/Plugins/UniTask/Runtime/UniTask.asmdef`): удалён `UNITASK_PHYSICS_SUPPORT`. Async-триггеров 3D-физики нет, 2D остались.
- **`Internal`:** удалён `RotatableAnimationAsset` и расширения на `AnimationClip`. Это был мёртвый код, но он держал модуль Animation.
- **`NetworkConnection.CreateWebSocket()`:** `DefaultWebSocket` (`ClientWebSocket`) скрыт под `#if UNITY_EDITOR || !UNITY_WEBGL`. В WebGL используется только `JsWebSocket`. Без этого в билд попадали `System.Net`, TLS, X509 и `Mono.Security`.
- **Graphics Settings:** шейдеры Built-in RP (Deferred, DeferredReflections, ScreenSpaceShadows, DepthNormals, MotionVectors, LightHalo, LensFlare) переведены в `Disabled`. URP их не использует.
- **Physics:** GameObject SDK = None. 3D-физика не инициализируется, в игре только 2D (Box2D).
- **Web Stripping Tool:** настройки в `Assets/Settings/DefaultSubmoduleStrippingSettings.asset`, стриппинг запускается автоматически после билда. В списке остались Freetype2, Tilemap, WebGPU Rendering, AndroidJNI, Newtonsoft и IAP. PhysX, Animation и UI Toolkit в нём больше не нужны: после удаления модулей их кода в wasm нет.

## Ограничения

- **Пост-обработку и эффекты URP не вырезать.** Ресурсы `PostProcessData` (`AreaTex`, шейдеры FinalPost, LensFlare, TAA, SMAA и т.п.) весят ~100 KB br, но пост-обработка появится в проекте позже.
- **Не возвращать `link.xml` с `preserve="all"`** на собственные сборки: он отключает стриппинг кода. Генератор `LinkerGenerator` закомментирован.

## При обновлении Unity или пакетов

Встроенные пакеты не обновляются вместе с редактором. Порядок действий:

1. Закрыть Unity.
2. Удалить `Packages/com.unity.ugui`, `Packages/com.unity.render-pipelines.universal` и `Packages/com.unity.render-pipelines.core`.
3. Открыть Unity, чтобы новые версии скачались в `Library/PackageCache`, и закрыть снова.
4. Скопировать пакеты из `Library/PackageCache/<пакет>@<хеш>` в `Packages/<пакет>`, исключив `Documentation~`, `Samples~` и `Tests` (с `Tests.meta`):
   ```bash
   rsync -a --exclude 'Documentation~' --exclude 'Samples~' --exclude '/Tests' --exclude '/Tests.meta' \
       Library/PackageCache/com.unity.ugui@<hash>/ Packages/com.unity.ugui/
   ```
5. Повторить правки asmdef из таблицы выше. Проверить, не появились ли новые versionDefines на `com.unity.modules.uielements`, `physics`, `animation` или `xr`.
6. Открыть Unity и собрать билд.

> [!warning] Устаревший импорт asmdef
> Если править asmdef при открытом Unity сразу после копирования пакета, Unity может скомпилировать сборку со старым импортом, и define останется. Правьте при закрытом редакторе или переимпортируйте asmdef принудительно: `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)`. Проверка: в `Library/Bee/artifacts/<dag>/<Assembly>.rsp` не должно быть удалённого `-define:`.

## Как проверить, что тянет модуль в билд

- **Причины подключения модулей:** `Library/Bee/artifacts/WebGL/ManagedStripped/wasm/UnityLinkerToEditorData.json`, раздел `report.modules[].dependencies`.
  - `dependencyType 0` — нативный класс используется кодом или ассетами;
  - `2` — модуль нужен другому модулю;
  - `3` — модуль обязателен всегда.
- **Какие сборки остались после стриппинга:** `Library/Bee/artifacts/WebGL/ManagedStripped/wasm/*.dll`. Кто ссылается на модуль, видно по `TypeReference` в этих сборках (например, через `System.Reflection.Metadata`).
- **Нативные классы из сцен и ассетов:** `Library/Bee/artifacts/UnityLinkerInputs/EditorToUnityLinkerData.json`, раздел `typesInScenes`.
