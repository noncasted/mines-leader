# Убрать Odin Inspector и NaughtyAttributes

## Контекст

Клиент тянет два инспектор-плагина: `Assets/Plugins/Sirenix` (15 МБ) и `Assets/Plugins/NaughtyAttributes` (1.4 МБ). Реальная поверхность использования в коде проекта крошечная, и почти всё либо выбрасывается, либо заменяется тремя своими атрибутами.

### Полная инвентаризация (проверено grep'ом по `client/Assets`, без `Plugins/`)

**Odin (`Sirenix.OdinInspector`) — 28 файлов проекта:**

| Что | Сколько | Решение |
|---|---|---|
| `[InlineEditor]` (на классе SO) | 19 | **удалить**, замены не нужно |
| `[Button("...")]` | 7 | свой `ButtonAttribute` |
| `[Sirenix.OdinInspector.Button]` | 2 (`GameRandom.cs:140,146`) | свой `ButtonAttribute` |
| `[Sirenix.OdinInspector.MinMaxSlider(a, b)]` на `Vector2` | 2 (`CardLocalDropOptions.cs:13`, `CardRemoteDropOptions.cs:13`) | свой `MinMaxSliderAttribute` + drawer |
| `[ShowInInspector, HideLabel]` над `Dictionary` | 1 (`SerializableDictionary.cs`) | **файл удаляется целиком** |
| `OdinMenuEditorWindow`, `SirenixEditorGUI`, `GUIHelper`, `AssemblyUtilities`, `GUILayoutOptions`, `IsNullOrWhitespace()`, `SplitPascalCase()`, `AlignCenter()` | 1 файл (`ScriptableObjectCreator.cs`) | **файл удаляется целиком** |

**NaughtyAttributes — 15 файлов проекта:**

| Что | Сколько | Решение |
|---|---|---|
| `[CurveRange]` / `[CurveRange(x0,y0,x1,y1)]` | 33 в 15 файлах | свой `CurveRangeAttribute` + drawer |
| больше ничего | — | — |

Проверено отдельно: `ShowIf`, `HideIf`, `Expandable`, `Foldout`, `Required`, `ReadOnly`, `OnValueChanged`, `Label`, `BoxGroup`, `Dropdown`, `InfoBox`, `ReorderableList`, `ValidateInput`, `ShowNonSerializedField` и остальные NA-атрибуты в коде проекта **не встречаются ни разу** (хиты грепа шли только из файлов самих плагинов).

### Почему удаление безопасно

- Нет `SerializedMonoBehaviour` / `SerializedScriptableObject` / `OdinValueDrawer` / `OdinAttributeProcessor` — Odin-сериализация не используется.
- Ни в одном `.asset` / `.prefab` / `.unity` нет `SerializationData` / `OdinSerializedData` → данные ассетов при удалении не теряются.
- Ни один `asmdef` проекта не ссылается на Sirenix или NaughtyAttributes явно. `NaughtyAttributes.Core` подключён через `autoReferenced: true`, Odin — через auto-reference DLL. Правок графа сборок не потребуется.
- `SerializableDictionary<,>` и `ScriptableObjectCreator` нигде не используются: ни в коде, ни в ассетах, ни в префабах, ни в сценах. Единственная потеря — пункт меню `Assets/Create Scriptable Object`.

### Куда класть новый код

- Атрибуты (runtime): `Assets/Common/Internal/Runtime/...` → сборка **`Internal`** (GUID `f039cb8c565843dba6da5b3c1ccde7d6`). Её уже явно реферят `Global` и `GamePlay`, то есть атрибуты будут видны во всех затронутых файлах.
- Drawer'ы и инспектор (editor): `Assets/Common/Internal/Editor/...` → сборка **`Internal.Editor`** (Editor-only, `autoReferenced: true`, уже реферит `Internal`). `CustomPropertyDrawer` и `CustomEditor` регистрируются глобально, так что типы из `GamePlay` и `Global` будут обслужены.

Предлагаемые пути:

```
Assets/Common/Internal/Runtime/Common/Attributes/CurveRangeAttribute.cs
Assets/Common/Internal/Runtime/Common/Attributes/MinMaxSliderAttribute.cs
Assets/Common/Internal/Runtime/Common/Attributes/ButtonAttribute.cs
Assets/Common/Internal/Editor/Attributes/CurveRangeDrawer.cs
Assets/Common/Internal/Editor/Attributes/MinMaxSliderDrawer.cs
Assets/Common/Internal/Editor/Attributes/ButtonsInspector.cs
```

Namespace — `Internal`, как у остального кода в этих сборках.

## Правила для исполнителя

1. **Один этап = один коммит.** Не переходить к следующему, пока предыдущий не собирается.
2. После каждого этапа — `dotnet build client/client.sln` (см. memory `client-headless-build-validates-di`: сборка гоняет ContainerGenerator, `CINGR00*` = сломанная регистрация). Это ловит все ошибки компиляции, включая editor-сборки.
3. Плагины удаляются **последними** (этап 5), чтобы на промежуточных этапах проект оставался собираемым.
4. Удалять папки вместе с `.meta`-файлами.
5. Финальную визуальную проверку инспекторов в Editor делает человек — см. чеклист в этапе 6.

## Этап 0. Свои атрибуты и drawer'ы

Написать три атрибута и их отрисовку. За основу брать исходники NaughtyAttributes (они остаются на диске до этапа 5):

- `Plugins/NaughtyAttributes/Scripts/Editor/PropertyDrawers/CurveRangePropertyDrawer.cs`
- `Plugins/NaughtyAttributes/Scripts/Editor/PropertyDrawers/MinMaxSliderPropertyDrawer.cs`
- `Plugins/NaughtyAttributes/Scripts/Editor/Utility/ButtonUtility.cs`, `NaughtyInspector.cs`

### `CurveRangeAttribute`

Нужны ровно два конструктора — других вызовов в проекте нет:

```csharp
[CurveRange]                       // без ограничений диапазона
[CurveRange(0, 0, 1, 2)]           // minX, minY, maxX, maxY
```

`EColor` из NA не переносим — цвет кривой нигде не задаётся. Использовать `Color.green`, как делает NA по умолчанию.

Drawer: `[CustomPropertyDrawer(typeof(CurveRangeAttribute))]`, внутри `EditorGUI.CurveField(rect, property, color, ranges, label)`, где `ranges = new Rect(minX, minY, maxX - minX, maxY - minY)`. Для параметрless-варианта передавать пустой `Rect` (NA хранит `Min = Vector2.zero`, `Max = Vector2.one` только при явных аргументах; проверить, что без аргументов `CurveField` получает `default(Rect)` и не режет кривую). Если `propertyType != AnimationCurve` — рисовать дефолт и `HelpBox`.

### `MinMaxSliderAttribute`

```csharp
[MinMaxSlider(300, 400)]
```

Только `float min, float max`, только для `Vector2` (Vector2Int-ветку из NA можно не тащить — в проекте её нет, но и вреда от неё нет). Drawer повторяет раскладку NA: label | float-поле min | `EditorGUI.MinMaxSlider` | float-поле max, с клампами по краям и через `EditorGUI.BeginProperty`/`EndProperty`.

### `ButtonAttribute` + инспектор

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ButtonAttribute : Attribute
{
    public string Text { get; }
    public ButtonAttribute(string text = null) => Text = text;
}
```

Инспектор: два класса с `[CanEditMultipleObjects]` — `[CustomEditor(typeof(MonoBehaviour), true)]` и `[CustomEditor(typeof(ScriptableObject), true)]` (нужны оба: `ColorCatalog` — это `ScriptableObject`, остальные — `MonoBehaviour`). В `OnInspectorGUI()`:

1. `DrawDefaultInspector()`;
2. собранные в `OnEnable` через рефлексию методы с `ButtonAttribute` (включая приватные и унаследованные, `BindingFlags.Instance | Public | NonPublic | FlattenHierarchy`) рисовать как `GUILayout.Button(attr.Text ?? ObjectNames.NicifyVariableName(method.Name))`;
3. по нажатию — `method.Invoke(target, null)` для каждого из `targets`, обёрнутое в `Undo.RecordObject` + `EditorUtility.SetDirty`.

Методы без параметров — все 9 текущих подходят. Учесть, что два из них называются `OnValidate` (`ScopeEntityView`, `DesignTextColorSetter`) — это валидно, просто вызываем как обычный метод.

**Риск:** глобальный `CustomEditor` перехватывает дефолтный инспектор всех MonoBehaviour/ScriptableObject проекта. Убедиться, что никакие собственные `CustomEditor` в `Internal.Editor` / `GamePlay.Editor` не перестали применяться (у более специфичного типа приоритет выше, так что не должны).

**Готово, когда:** новые файлы компилируются, старые плагины ещё на месте, проект собирается.

## Этап 1. `[InlineEditor]` — удалить

19 вхождений в 19 файлах. Убрать строку `[InlineEditor]` и, если `using Sirenix.OdinInspector;` больше не нужен в файле, убрать и его.

Файлы: `ScriptableCurve`, `FloatValue`, `ForwardAnimationAsset`, `DesignColor`, `DefaultElementConfig`, `NestedElementConfig`, `ElementScaleConfig`, `BoardsRevealOptions`, `CardDroppedOptions`, `CardDragOptions`, `CardIdleOptions`, `CardLocalSpawnOptions`, `CardLocalStashOptions`, `CardLocalDropOptions`, `CardRemoteDropOptions`, `CardRemoteIdleOptions`, `CardRemoteSpawnOptions`, `CardRemoteStashOptions`, `HandPositionsOptions`.

Замечание: почти все эти SO грузятся через `GamePlayAssets` / `AssetCatalog`, а не через сериализованные поля, поэтому `[InlineEditor]` там и так ни на что не влиял. Сериализованных полей этих типов всего 5-6 — потеря инлайн-редактирования затронет только их (`HandPositions._options`, `DesignElementScale._config`, `DesignElementColorSetter._color`, `DesignTextColorSetter._color`, `NestedElementConfig._source`, `SerializableCurve._time`).

## Этап 2. Удалить мёртвый код

- `Assets/Common/Internal/Runtime/Common/DataTypes/Collections/SerializableDictionary.cs` (+ `.meta`)
- `Assets/Common/Internal/Editor/Tools/ScriptableObjects/ScriptableObjectCreator.cs` (+ `.meta`, при необходимости пустые папки)

Перед удалением ещё раз прогнать поиск по `.cs`, `.asset`, `.prefab`, `.unity` — на момент составления задачи оба не используются нигде.

С `ScriptableObjectCreator` уходит пункт меню `Assets/Create Scriptable Object`; остаются штатные `[CreateAssetMenu]` (7 штук) через `Assets/Create/...`.

## Этап 3. Переключить `[Button]` и `[MinMaxSlider]`

9 мест с `Button`:

| Файл | Строка |
|---|---|
| `Common/Internal/Runtime/Catalogues/Colors/ColorCatalog.cs` | 33 |
| `Common/Internal/Runtime/Scopes/Entities/ScopeEntityView.cs` | 34 |
| `Common/Internal/Runtime/Tools/HierarchyBindings/ObjectBindings.cs` | 38 |
| `Common/Global/UI/Design/Elements/DesignTextColorSetter.cs` | 13 |
| `Common/Global/UI/Design/Elements/DesignElement.cs` | 61 |
| `GamePlay/Boards/Root/BoardConstructor.cs` | 88, 124 |
| `GamePlay/Cards/Entities/Actions/Random/GameRandom.cs` | 140, 146 |

2 места с `MinMaxSlider`: `CardLocalDropOptions.cs:13`, `CardRemoteDropOptions.cs:13` — заменить `[Sirenix.OdinInspector.MinMaxSlider(...)]` на `[MinMaxSlider(...)]`.

В `ColorCatalog` атрибут стоит под `#if UNITY_EDITOR` вместе с `using Sirenix.OdinInspector;` — новый `ButtonAttribute` из `Internal` runtime-сборки, так что `using` там просто уходит, а `#if` вокруг метода остаётся (он дёргает `EditorApplication`).

**Готово, когда:** в коде проекта не осталось ни одного `using Sirenix.OdinInspector;` и ни одного `Sirenix.`-квалифицированного имени.

## Этап 4. Переключить `[CurveRange]`

33 вхождения в 15 файлах — заменить `using NaughtyAttributes;` на `using Internal;` (или убрать вовсе там, где `Internal` уже подключён). Сам синтаксис атрибута не меняется.

Файлы: `Curve.cs`, `ScriptableCurve.cs`, `SerializableCurve.cs`, `BoardsRevealOptions.cs`, `GameRandom.cs`, `CardDroppedOptions.cs`, `CardLocalDropOptions.cs`, `CardIdleOptions.cs`, `CardLocalSpawnOptions.cs`, `CardLocalStashOptions.cs`, `CardRemoteDropOptions.cs`, `CardRemoteSpawnOptions.cs`, `CardRemoteStashOptions.cs`, `HandPositionsOptions.cs`, `GameFloatingText.cs`.

**Готово, когда:** в коде проекта не осталось ни одного `using NaughtyAttributes;`.

## Этап 5. Удалить плагины

- `Assets/Plugins/Sirenix/` + `Assets/Plugins/Sirenix.meta` (15 МБ, включая модули `Unity.Addressables` и `Unity.Mathematics` с их asmdef)
- `Assets/Plugins/NaughtyAttributes/` + `.meta` (1.4 МБ)
- `client/Sirenix.OdinInspector.Modules.Unity.Addressables.csproj` (+ `.DotSettings`)
- `client/Sirenix.OdinInspector.Modules.UnityMathematics.csproj` (+ `.DotSettings`)
- почистить `client/obj/Debug/Sirenix.*` (мусор сборки, в git может не быть)

Проверить, не остались ли ссылки на эти csproj в `client/client.sln` и в `.sln`-файлах, которые Unity регенерирует.

Дополнительно проверить (на момент составления задачи не найдено, но Odin иногда добавляет): define `ODIN_INSPECTOR` / `ODIN_VALIDATOR` в `ProjectSettings/ProjectSettings.asset`, папку `ProjectSettings/Odin Inspector`, записи в `*.rsp` и `link.xml`.

## Этап 6. Проверка

1. `dotnet build client/client.sln` — чисто, без `CINGR00*`.
2. Открыть проект в Unity, дождаться реимпорта, консоль без ошибок и без `Missing script`.
3. Глазами в инспекторе:
   - `AnimationCurve`-поля с `[CurveRange]` рисуются кривой с нужным диапазоном (взять `HandPositionsOptions` — там 5 разных диапазонов, включая отрицательные);
   - `_twistAngleRange` в `CardLocalDropOptions` / `CardRemoteDropOptions` — min-max слайдер с двумя полями;
   - кнопки на `BoardConstructor` (Preview / Clear), `ColorCatalog` (Refresh), `DesignElement` (Scan behaviours), `GameRandom` (DebugDice / DebugCoin) — видны и работают;
   - дефолтные инспекторы обычных MonoBehaviour не сломались глобальным `CustomEditor`.
4. Собрать WebGL-билд и сверить размер (ожидаем небольшое уменьшение — Odin runtime-DLL перестанут попадать в билд). См. memory `webgl-build-size-work`.

## Прогресс

### Этапы 0-5 — 2026-09-20

Сделано, `dotnet build client/client.sln` чистый (0 errors, без `CINGR00*`). Коммиты:

- `[Client] Add own Button, CurveRange and MinMaxSlider attributes` — три атрибута в `Internal`, drawer'ы и `ButtonsInspector` в `Internal.Editor`.
- `[Client] Drop Odin [InlineEditor] from scriptable objects` — 19 файлов.
- `[Client] Remove Odin-based ScriptableObjectCreator window`.
- `[Client] Switch [Button] and [MinMaxSlider] to own attributes` — `Sirenix` в коде проекта больше нет.
- `[Client] Switch [CurveRange] to own attribute` — `NaughtyAttributes` в коде проекта больше нет.
- `[Client] Delete Odin Inspector and NaughtyAttributes` + `[Client] Drop Odin and NaughtyAttributes projects from client.slnx`.

Отличия от плана и находки:

- `SerializableDictionary.cs` из этапа 2 в репозитории уже отсутствовал — удалён только `ScriptableObjectCreator`.
- `[CurveRange]` без аргументов теперь рисуется без ограничения диапазона (`default(Rect)`), как и написано в задаче. NA в этом случае зажимал кривую в `0..1` — если где-то это было важно, ставить явный `[CurveRange(0, 0, 1, 1)]`.
- Нашлись ссылки, которых не было в инвентаризации: `NaughtyAttributes.Core` был прописан в `references` у `Internal.asmdef` и `GamePlay.asmdef` (убрано), а `Sirenix.*.dll` — в `precompiledReferences` у `Packages/com.singularitygroup.hotreload/Editor/SingularityGroup.HotReload.Editor.asmdef` (убрано; Odin-код там под `#if ODIN_INSPECTOR`).
- Из `ProjectSettings.asset` убраны дефайны `ODIN_INSPECTOR*` для Standalone и WebGL. Папки `ProjectSettings/Odin Inspector`, `*.rsp` и ссылок в `link.xml` не было.
- `Assets/Plugins` после удаления — 9.0 МБ.

### Этап 6 — осталось человеку

1. Открыть проект в Unity, дождаться реимпорта, проверить консоль.
2. Визуальный чеклист инспекторов из этапа 6.
3. WebGL-билд и сверка размера.
