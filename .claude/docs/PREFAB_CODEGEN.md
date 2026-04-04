# Prefab Code Generation

## Overview

Prefabs in this project can be defined entirely in C# code using `PrefabBuilder`. This eliminates manual prefab editing in Unity Editor and makes prefab structure version-controllable, reviewable, and reproducible.

Generated prefabs are saved to `Assets/Resources/Generated/` and accessible at runtime via the `Prefabs` static class (auto-generated).

## How It Works

1. A class marked with `[PrefabDefinition]` defines prefab structure in a static `Define(PrefabBuilder)` method
2. `PrefabGenerator` (editor-only) finds all `[PrefabDefinition]` classes on domain reload and via `Tools/GeneratePrefabs` menu
3. Each definition creates a temporary GameObject hierarchy, configures components, and saves as `.prefab`
4. `PrefabsClassGenerator` auto-generates `Prefabs.cs` with lazy-loaded `StaticPrefab` properties
5. Stale prefabs (files in `Generated/` not matching any current definition) are automatically deleted

## PrefabBuilder API

### Core

```csharp
builder.WithName("MyPrefab")                          // set prefab name (required)
builder.WithComponent<T>(Action<T> configure)          // add component + configure
builder.WithComponent<T>(out T component)              // add component + get reference
builder.WithChild("Name")                              // create child GO, returns GameObject
builder.WithChild<T>("Name")                           // create child GO with component T
builder.WithChildObject("Name", Action<PrefabBuilder>) // nested builder for deep hierarchies
builder.SetSerialized<T>("_fieldName", value)          // set private serialized field via SerializedObject
builder.Build("path.prefab")                           // save as prefab asset
```

### Transform

```csharp
builder.WithPosition(x, y, z)    // set localPosition
builder.WithScale(x, y, z)       // set localScale
builder.WithRotation(x, y, z)    // set localEulerAngles
```

### Utilities

```csharp
builder.GameObject                              // access underlying GameObject for cross-references
PrefabBuilder.LoadAsset<T>("Assets/path.ext")   // load asset by path (editor-only)
```

### SetSerialized Supported Types

- `string`, `int`, `float`, `bool`
- `Color`, `Vector2`, `Vector3`
- `Enum` (as int index)
- `ObjectReference` (any UnityEngine.Object: Component, GameObject, Sprite, Material, TMP_FontAsset, etc.)

## Converting a Prefab to Code

### Step 1: Analyze the original prefab via Unity MCP

Use MCP tools to inspect the prefab hierarchy and component properties:

```
manage_prefabs(action="get_hierarchy", prefab_path="Assets/.../MyPrefab.prefab")
manage_prefabs(action="open_prefab_stage", prefab_path="...")
find_gameobjects(search_term="ObjectName", search_method="by_name")
ReadMcpResourceTool(uri="mcpforunity://scene/gameobject/{id}/components")
manage_prefabs(action="close_prefab_stage")
```

Key data to collect for each object:
- **Transform**: localPosition, localScale, localEulerAngles
- **SpriteRenderer**: sprite path, color, sortingLayerName, sortingOrder, flipX/flipY
- **TextMeshPro**: font path, color, fontSize, enableAutoSizing, fontSizeMin/Max, alignment, lineSpacingAdjustment
- **RectTransform**: anchoredPosition, sizeDelta
- **Custom components**: all serialized field values and cross-references
- **Colliders**: size, offset
- **SortingGroup**: sortingLayerName, sortingOrder

### Step 2: Create the `[PrefabDefinition]` class

Place it near the related code (e.g., card prefab definitions near card code).

Pattern for cross-references between components:

```csharp
// Capture references via closures (not out params — those can't be used in lambdas)
SortingGroup sortingGroup = null;

builder.WithComponent<SortingGroup>(sg => {
    sg.sortingLayerName = "Cards";
    sortingGroup = sg;
});
builder.WithComponent<CardRenderer>();
builder.SetSerialized<CardRenderer>("_sortingGroup", sortingGroup);
```

Pattern for child-to-parent references:

```csharp
TextMeshPro nameText = null;

body.WithChildObject("Name", name => {
    name.WithComponent<TextMeshPro>(tmp => {
        tmp.font = PrefabBuilder.LoadAsset<TMP_FontAsset>("Assets/.../Font.asset");
        nameText = tmp;  // capture for parent
    });
});

// After WithChildObject returns, nameText is set (synchronous execution)
body.SetSerialized<CardDataView>("_name", nameText);
```

### Step 3: Verify

1. Trigger recompilation or run `Tools/GeneratePrefabs`
2. Check Unity console for errors
3. Compare generated prefab hierarchy with original via MCP `get_hierarchy`
4. Compare component properties via MCP component resources

## Existing Definitions

| Definition | File | Prefab |
|---|---|---|
| `CardBasePrefab` | `GamePlay/Cards/Options/Prefabs/CardPrefabDefinitions.cs` | Card_Base |
| `CardLocalPrefab` | same | Card_Local |
| `CardRemotePrefab` | same | Card_Remote |
| `AudioPlayerPrefab` | `Global/Audio/GlobalAudioExtensions.cs` | Global_Audio_Player |
| `AudioListenerPrefab` | `Global/Audio/GlobalAudioExtensions.cs` | Global_Audio_Listener |
| `GlobalCameraPrefab` | `Global/Cameras/GlobalCameraExtensions.cs` | Global_Camera |
| `GlobalUpdaterPrefab` | `Global/Systems/GlobalSystemExtensions.cs` | GlobalUpdater |
| `GlobalEventSystemPrefab` | `Global/Inputs/GlobalInputExtensions.cs` | Global_Events |

## Runtime Usage

```csharp
// Access via static Prefabs class
var go = Prefabs.CardLocal.Value;                          // GameObject
var entity = Prefabs.CardLocal.As<CardScopeEntity>();      // typed component
```

## Gotchas

- `ScopeEntityView.OnValidate()` fires during `AddComponent` — fields must handle null (fixed with `??= new()`)
- TextMeshPro auto-adds RectTransform — don't add it manually, configure it after adding TMP
- `SetSerialized` uses serialized field names (underscore-prefixed private fields), not property names
- Lambdas inside `WithChildObject`/`WithComponent` execute synchronously — captured variables are available after the call
- `out` params can't be used inside lambdas — use closure capture instead
- `[PrefabDefinition]` classes MUST be wrapped in `#if UNITY_EDITOR` / `#endif` — PrefabBuilder is editor-only and won't compile in builds
- Renaming `WithName()` creates a new prefab file — the old one is auto-deleted by stale cleanup on next generation
