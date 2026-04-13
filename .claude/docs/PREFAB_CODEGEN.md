# PrefabBuilder System

Система программного создания Unity-префабов через fluent API. Работает только в Editor (`#if UNITY_EDITOR`).

## Архитектура

```
Assets/Tools/PrefabBuilder/
  Runtime/
    PrefabBuilder.cs                          -- ядро билдера (data class)
    StaticPrefab.cs                           -- lazy-загрузка префабов
    Prefabs.cs                                -- авто-генерируемый реестр
    PrefabDefinitionAttribute.cs              -- маркер [PrefabDefinition]
    PrefabsExtensions.cs                      -- As<T>()
    AssetsBuilderExtensions.cs                -- FromPrefab, LoadAsset, LoadSubAsset
    DI/
      InjectGeneratedAttribute.cs             -- [InjectGenerated] для авто-инъекции
      PrefabBuilderContainer.cs               -- DI контейнер для cross-references
      SerializationBuilderExtensions.cs       -- SetSerialized, Build, serialization
    Objects/
      ObjectComponentsBuilderExtensions.cs    -- WithComponent, Register, GetComponent
      ObjectHierarchyBuilderExtensions.cs     -- WithChild, WithChildObject, WithName, WithActive, WithPrefabChild
      TransformBuilderExtensions.cs           -- WithPosition, WithScale, WithRotation
    UI/
      RectTransformBuilderExtensions.cs       -- WithRectTransform, StretchFull, anchors, sizing
      MpImageBuilderExtensions.cs             -- WithMPImage, WithRoundedRect, WithCircle, WithOutline
      TextBuilderExtensions.cs                -- AddLabel (TextMeshPro)
      InputFieldBuilderExtensions.cs          -- WithNodeInputField (visual-only input)
      ResponsiveContainerBuilderExtensions.cs -- WithResponsiveContainer, layout
  Editor/
    PrefabGenerator.cs                        -- сканирует [PrefabDefinition], генерирует
    PrefabsClassGenerator.cs                  -- генерирует Prefabs.cs
```

### Required usings

```csharp
using Tools;            // PrefabBuilder, AssetsBuilderExtensions, Prefabs
using Tools.Objects;    // WithComponent, WithChild, WithChildObject, WithName, WithPosition...
using Tools.DI;         // SetSerialized, Build, InjectGenerated, PrefabBuilderContainer
using Tools.UI;         // WithRectTransform, WithMPImage, AddLabel, WithResponsiveContainer
```

---

## PrefabBuilder (Core API)

### Создание

```csharp
// Новый пустой билдер
var builder = new PrefabBuilder();

// Из существующего GameObject (поле builder.GameObject)
var builder = PrefabBuilder.FromGameObject(existingGo);
var builder = PrefabBuilder.FromGameObject(existingGo, container);

// Из существующего префаба (копирует)
var builder = AssetsBuilderExtensions.FromPrefab("Assets/Path/To.prefab");
```

### Fluent-методы (все возвращают PrefabBuilder)

```csharp
builder
    .WithName("MyPrefab")                      // установить имя (обязательно)
    .WithPosition(x, y, z)                     // localPosition
    .WithScale(x, y, z)                        // localScale
    .WithRotation(x, y, z)                     // localEulerAngles
    .WithActive(true)                          // SetActive
    .WithRectTransform(rt => rt.StretchFull()) // добавить/настроить RectTransform
    .WithComponent<T>()                        // добавить компонент
    .WithComponent<T>(c => c.Setup())          // добавить + настроить
    .WithComponent<T>(out var component)       // добавить + захватить ссылку
    .WithComponent<T>("key")                   // добавить + зарегистрировать в DI с ключом
    .SetSerialized<T>("_fieldName", value)     // установить serialized поле
    .Register(value)                           // зарегистрировать в DI (по runtime типу)
    .Register(value, "key")                    // зарегистрировать с ключом
    .Register<T>(value, "key")                 // зарегистрировать по явному типу
```

### Создание дочерних объектов

```csharp
// Простой child
builder.WithChild("Name");                     // -> GameObject
builder.WithChild<T>("Name");                  // -> T component
builder.WithChild<T>("Name", c => c.Setup());  // -> T component + настройка

// Составной child с вложенным билдером
builder.WithChildObject("Name", child => {
    child
        .WithRectTransform(rt => rt.StretchFull())
        .WithComponent<CanvasRenderer>()
        .WithMPImage(Color.white);
});

// Перегрузки WithChildObject:
WithChildObject(name, configure)
WithChildObject(name, active, configure)
WithChildObject(name, parent, configure)              // parent = Transform
WithChildObject(name, parent, active, configure)

// Безымянный child
builder.WithChild(child => { ... });

// Деактивировать текущий объект
builder.Disable();

// Вложить существующий префаб
builder.WithPrefabChild("Assets/Path/To.prefab", "OptionalName");
```

### Загрузка ассетов

```csharp
var asset = AssetsBuilderExtensions.LoadAsset<T>("Assets/Path/To.asset");
var sub = AssetsBuilderExtensions.LoadSubAsset<T>("Assets/Path/To.asset", "SubAssetName");
```

### Сборка

```csharp
GameObject prefab = builder.Build("Assets/Resources/Generated/MyPrefab.prefab");
// ResolveAll() DI -> ApplySerializedProperties -> SaveAsPrefabAsset -> DestroyImmediate
```

---

## DI-система (InjectGenerated)

Ключевой паттерн для связывания UI-элементов с [SerializeField] полями без string-based SetSerialized:

### Атрибут [InjectGenerated]

```csharp
public class MyView : MonoBehaviour {
    [SerializeField, InjectGenerated] private SpriteRenderer _renderer;
    [SerializeField, InjectGenerated("label")] private TMP_Text _label;
}
```

### Регистрация в PrefabDefinition

```csharp
// Option 1: WithComponent с ключом (preferred для компонентов)
builder.WithComponent<Button>("submitBtn");    // добавляет + регистрирует с ключом

// Option 2: явная регистрация (для out params, загруженных ассетов)
builder.Register(spriteRenderer);              // по runtime типу, пустой ключ
builder.Register<TMP_Text>(label, "label");    // по явному типу + ключ
```

### Как работает

1. `WithComponent<T>()` автоматически вызывает `Container.AddTarget(component)` -- компонент становится кандидатом для инъекции
2. `Register()` регистрирует значения по типу + ключу в контейнере
3. `Build()` вызывает `Container.ResolveAll()` -- обходит все target-поля с `[InjectGenerated]`, резолвит из реестра
4. Если зависимость не найдена -- `InvalidOperationException` с диагностикой

### Правила

- `[InjectGenerated]` без ключа резолвит с ПУСТОЙ строкой ключа
- `[InjectGenerated("myKey")]` резолвит с ключом `"myKey"`
- Ключ в WithComponent/Register ДОЛЖЕН совпадать с ключом в InjectGenerated
- Register(value) регистрирует по runtime типу + все базовые типы (до MonoBehaviour)

---

## Extension-методы UI

### RectTransform (namespace Tools.UI)

Все возвращают `RectTransform` для чейнинга:

```csharp
// Stretch
rt.StretchFull()              // заполнить родителя целиком
rt.StretchHorizontal()        // растянуть по X
rt.StretchVertical()          // растянуть по Y

// Anchor к краю
rt.WithAnchorTop()            // прижать к верху
rt.AnchorBottom()             // прижать к низу
rt.AnchorLeft()               // прижать к левому краю
rt.AnchorRight()              // прижать к правому краю
rt.AnchorTopLeft()            // точечный якорь в верхнем левом углу

// Размеры
rt.WithWidth(100)
rt.WithHeight(50)
rt.WithSize(100, 50)
rt.WithSize(new Vector2(100, 50))

// Отступы (от stretch)
rt.WithPadding(4)             // uniform
rt.WithPadding(8, 4)          // horizontal, vertical

// Позиция
rt.WithPosition(x, y)

// Pivot
rt.WithPivot(x, y)
rt.TopPivot()                 // (0.5, 1)
rt.BottomPivot()              // (0.5, 0)
rt.CenterPivot()              // (0.5, 0.5)
```

### MPImage (namespace Tools.UI)

```csharp
// Базовый (авто-добавляет CanvasRenderer)
builder.WithMPImage(color)
builder.WithMPImage(color, out MPImage image)
builder.WithMPImage(configure)
builder.WithCornerRadius(12f)
builder.WithCornerRadius(new Vector4(top, right, bottom, left))

// Скругленный прямоугольник
builder.WithRoundedRect(color, cornerRadius)
builder.WithRoundedRect(color, Vector4 cornerRadius)
builder.WithRoundedRect(color, cornerRadius, out MPImage image)

// Круг
builder.WithCircle(color)
builder.WithCircle(color, out MPImage image)

// Обводка (заливка + рамка)
builder.WithOutline(fillColor, outlineColor, outlineWidth)

// Stroke (пустая фигура)
builder.WithStroke(strokeColor, strokeWidth)

// MPImage на дочернем объекте
builder.WithMPImageChild("Name", color, child => { ... })
builder.WithMPImageChild("Name", active, color, child => { ... })
builder.WithRoundedRectChild("Name", color, cornerRadius, child => { ... })
```

### TextMeshPro (namespace Tools.UI)

```csharp
builder.AddLabel("Label Name", "text")
builder.AddLabel("Label Name", "text", tmp => tmp.fontSize = 18)
builder.AddLabel("Label Name", "text", rt => rt.StretchFull())
builder.AddLabel("Label Name", "text", out TextMeshProUGUI label, rt => ...)
builder.AddLabel("Label Name", rt => ..., tmp => ...)
```

Дефолты для AddLabel:
- Color: (0.2, 0.2, 0.2, 1)
- FontSize: 14
- Alignment: Center
- OverflowMode: Ellipsis
- RaycastTarget: false

### ResponsiveContainer (namespace Tools.UI)

```csharp
builder.WithResponsiveContainer(rc => {
    rc.AsVertical()                              // или AsHorizontal()
      .WithHorizontalFitInContainer()            // FitToContent, Spread, Group
      .WithVerticalFitToContent()                // FitInContainer, Spread, Group, GroupAndExpand
      .WithHorizontalAlign(HAlignType.Center)
      .WithVerticalAlign(VAlignType.Top)
      .WithSpacing(4f)
      .WithMargins(top: 8, bottom: 8)
      .WithRefreshOnChange();
});

builder.WithResponsiveChild("Name", rc => rc.AsVertical(), child => { ... });
builder.ExcludeFromLayout();
builder.ResizeResponsive(recursive: true);
builder.RecalculateResponsiveContainers();
```

---

## Определение префабов

### Атрибут [PrefabDefinition]

Два варианта сигнатуры `Define()`:

```csharp
// Вариант 1: Generator создает builder, передает его
[PrefabDefinition]
public static class MyPrefab {
    public static void Define(PrefabBuilder builder) {
        builder.WithName("MyPrefab")
            .WithComponent<MyComponent>();
    }
}

// Вариант 2: Метод сам создает и возвращает builder (для derived префабов)
[PrefabDefinition]
public static class MyDerivedPrefab {
    public static PrefabBuilder Define() {
        var builder = AssetsBuilderExtensions.FromPrefab("Assets/Resources/Generated/Base.prefab");
        builder.WithName("MyDerived");
        return builder;
    }
}
```

### Генерация

Menu: `Tools > GeneratePrefabs`

- Сканирует все `[PrefabDefinition]` классы через reflection
- Сортирует: base (void Define(PB)) первые, derived (PB Define()) вторые
- Сохраняет в `Assets/Resources/Generated/{Name}.prefab`
- Генерирует `Prefabs.cs` с StaticPrefab для каждого
- Удаляет stale-префабы из Generated/

### Использование в рантайме

```csharp
var prefab = Prefabs.CardLocal;                          // StaticPrefab (lazy)
var go = Prefabs.CardLocal.Value;                        // GameObject
var entity = Prefabs.CardLocal.As<CardScopeEntity>();    // типизированный компонент

// Implicit conversion
GameObject go = Prefabs.CardLocal;
```

---

## SetSerialized: поддерживаемые типы

- `string`, `int`, `float`, `bool`
- `Color`, `Vector2`, `Vector3`, `Vector4`
- `Enum` (as int index)
- `ObjectReference` (any UnityEngine.Object)
- `AnimationCurve`
- `Array` (рекурсивная сериализация элементов)

---

## Паттерн: Cross-references через closures

```csharp
// Capture references via closures (not out params -- those can't be used in lambdas)
SortingGroup sortingGroup = null;

builder.WithComponent<SortingGroup>(sg => {
    sg.sortingLayerName = "Cards";
    sortingGroup = sg;
});
builder.WithComponent<CardRenderer>();
builder.SetSerialized<CardRenderer>("_sortingGroup", sortingGroup);
```

Паттерн для child-to-parent:

```csharp
TextMeshPro nameText = null;

body.WithChildObject("Name", name => {
    name.WithComponent<TextMeshPro>(tmp => {
        tmp.font = AssetsBuilderExtensions.LoadAsset<TMP_FontAsset>("Assets/.../Font.asset");
        nameText = tmp;  // capture for parent
    });
});

// After WithChildObject returns, nameText is set (synchronous execution)
body.SetSerialized<CardDataView>("_name", nameText);
```

---

## TMP_InputField (CRITICAL)

PrefabBuilder **CANNOT** create working `TMP_InputField`. Cross-references (`textViewport`, `textComponent`) are lost during prefab serialization.

### Solution: Two-Phase Pattern

**Phase 1 -- PrefabDefinition (visual only):**
```csharp
builder.WithNodeInputField("InputName", out var inputRect, out var inputText);
builder.Register<RectTransform>(inputRect, "inputRect");
builder.Register<TMP_Text>(inputText, "inputText");
```

**Phase 2 -- Runtime assembly:**
```csharp
// Disable GO -> add TMP_InputField -> set references -> re-enable
[SerializeField, InjectGenerated("inputRect")] private RectTransform _inputRect;
[SerializeField, InjectGenerated("inputText")] private TMP_Text _inputText;
private TMP_InputField _inputField; // runtime only, not serialized
```

---

## Gotchas

- `ScopeEntityView.OnValidate()` fires during `AddComponent` -- fields must handle null
- TextMeshPro auto-adds RectTransform -- don't add it manually, configure it after adding TMP
- `SetSerialized` uses serialized field names (underscore-prefixed private fields), not property names
- Lambdas inside `WithChildObject`/`WithComponent` execute synchronously -- captured variables are available after the call
- `out` params can't be used inside lambdas -- use closure capture instead
- `[PrefabDefinition]` classes should be wrapped in `#if UNITY_EDITOR` / `#endif` -- PrefabBuilder is editor-only
- Renaming `WithName()` creates a new prefab file -- the old one is auto-deleted by stale cleanup
- Prefer `[InjectGenerated]` + `Register()` over `SetSerialized` -- compile-time safe vs string-based
