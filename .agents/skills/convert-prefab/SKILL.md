---
name: convert-prefab
description: Convert a Unity prefab to code-generated [PrefabDefinition] using PrefabBuilder. Use this skill whenever the user asks to convert, codegen, or recreate a prefab from code. Also trigger when user says "prefab to code", "generate prefab", or mentions PrefabBuilder for a specific prefab.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /convert-prefab, convert-prefab, конвертируй префаб, prefab to code, generate prefab, префаб в код, кодген префаба, PrefabBuilder
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Convert Prefab to Code Skill

This skill converts an existing Unity prefab into a `[PrefabDefinition]` class that generates the prefab from C# code using `PrefabBuilder`. The generated prefab is saved to `Assets/Resources/Generated/` and accessible via `Prefabs.PropertyName`.

Reference: `.agents/docs/PREFAB_CODEGEN.md`

## Input

The user provides a prefab path or name. Examples:
- "convert Card_Local prefab to code"
- "codegen the Board prefab"
- "Assets/GamePlay/Something/MyPrefab.prefab"

If only a name is given, search for it:
```
manage_asset(action="search", path="Assets", search_pattern="PrefabName", filter_type="Prefab")
```

## Step 1: Analyze Prefab Structure via MCP

### 1a. Get hierarchy

```
manage_prefabs(action="get_hierarchy", prefab_path="Assets/.../MyPrefab.prefab")
```

Record for each object:
- Name, path in hierarchy, childCount
- componentTypes list
- Whether it's a variant (isVariant, parentPrefab)

### 1b. Get detailed component properties

Open prefab stage, then for each object get full component data:

```
manage_prefabs(action="open_prefab_stage", prefab_path="...")
find_gameobjects(search_term="ObjectName", search_method="by_name")
ReadMcpResourceTool(server="unityMCP", uri="mcpforunity://scene/gameobject/{id}/components")
```

For each object, record these essential properties:

**Transform/RectTransform:**
- localPosition (x, y, z)
- localScale (x, y, z)
- localEulerAngles (x, y, z)
- For RectTransform: anchoredPosition, sizeDelta

**SpriteRenderer:**
- sprite path (e.g. "Assets/GamePlay/.../sprite.psd")
- color (r, g, b, a)
- sortingLayerName, sortingOrder
- flipX, flipY
- drawMode

**TextMeshPro:**
- font path (e.g. "Assets/Common/Artwork/Font.asset")
- color (r, g, b, a)
- fontSize, enableAutoSizing, fontSizeMin, fontSizeMax
- horizontalAlignment, verticalAlignment
- textWrappingMode, lineSpacingAdjustment

**MeshRenderer:**
- sortingLayerName, sortingOrder
- material path

**BoxCollider2D:**
- size (x, y)
- offset

**SortingGroup:**
- sortingLayerName, sortingOrder

**Custom components:**
- All serialized field values
- Object references (note the referenced component type and which object it's on)

Close prefab stage when done:
```
manage_prefabs(action="close_prefab_stage")
```

### 1c. Identify cross-references

Map all serialized object references between components. For example:
- `CardRenderer._sortingGroup` -> SortingGroup on same GO
- `CardDataView._name` -> TextMeshPro on child "Name"

To know field types, read the source `.cs` file of each custom component. Look for `[SerializeField]` private fields.

## Step 2: Find the source files of custom components

For each custom MonoBehaviour component found:
```
Grep for "class ComponentName" in *.cs files
Read the file to find serialized fields and their types
```

This is critical for `SetSerialized` calls — you need exact field names and types.

## Step 3: Create the [PrefabDefinition] class

### File location
Place the definition near the related code (e.g., in the same file as the DI extensions that use the prefab). MUST wrap in `#if UNITY_EDITOR` / `#endif` — PrefabBuilder is editor-only and won't compile in builds.

### Template

```csharp
#if UNITY_EDITOR
using Tools;
using UnityEngine;
// ... other usings as needed

namespace MyNamespace
{
    [PrefabDefinition]
    public static class MyPrefab
    {
        public static void Define(PrefabBuilder builder)
        {
            // Root object
            builder.WithName("MyPrefab_Name");

            // Add root components
            builder.WithComponent<MyComponent>(c => { /* configure */ });

            // Cross-references via closure capture
            SortingGroup sg = null;
            builder.WithComponent<SortingGroup>(s => { sg = s; s.sortingLayerName = "..."; });
            builder.SetSerialized<OtherComponent>("_sortingGroup", sg);

            // Child hierarchy
            builder.WithChildObject("ChildName", child =>
            {
                child.WithPosition(x, y, z);
                child.WithScale(sx, sy, sz);
                child.WithComponent<SpriteRenderer>(sr =>
                {
                    sr.sprite = PrefabBuilder.LoadAsset<Sprite>("Assets/.../sprite.psd");
                    sr.color = new Color(r, g, b, a);
                    sr.sortingLayerName = "UI";
                    sr.sortingOrder = 1;
                });
            });
        }
    }
}
#endif
```

### Key patterns

**Cross-references (same object):**
```csharp
SortingGroup sg = null;
builder.WithComponent<SortingGroup>(s => sg = s);
builder.WithComponent<MyComp>();
builder.SetSerialized<MyComp>("_field", sg);
```

**Cross-references (parent to child):**
```csharp
SpriteRenderer childSr = null;
parent.WithChildObject("Child", child => {
    child.WithComponent<SpriteRenderer>(sr => childSr = sr);
});
parent.SetSerialized<ParentComp>("_childRef", childSr);
```

**TextMeshPro with RectTransform:**
```csharp
parent.WithChildObject("Label", label => {
    label.WithComponent<TextMeshPro>(tmp => {
        tmp.font = PrefabBuilder.LoadAsset<TMP_FontAsset>("Assets/.../Font.asset");
        tmp.color = new Color(r, g, b, a);
        tmp.fontSize = 6f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 3f;
        tmp.fontSizeMax = 72f;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
    });
    // RectTransform is auto-added by TextMeshPro
    var rt = label.GameObject.GetComponent<RectTransform>();
    if (rt != null) {
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }
    var mr = label.GameObject.GetComponent<MeshRenderer>();
    if (mr != null) {
        mr.sortingLayerName = "UI";
        mr.sortingOrder = 1;
    }
});
```

## Step 4: Verify compilation

```
refresh_unity(compile="request", scope="all", wait_for_ready=true)
read_console(action="get", types=["error"], count=10)
```

Fix any errors. Common issues:
- `ScopeEntityView.OnValidate()` NullRef — check `_autoDetected ??= new()` fix is in place
- Missing assembly reference — check .asmdef references include `Tools.PrefabBuilder`

## Step 5: Compare generated vs original

```
manage_prefabs(action="get_hierarchy", prefab_path="Assets/Resources/Generated/MyPrefab.prefab")
```

Verify:
- Same number of objects
- Same component types on each object
- Open both prefab stages and compare key properties (sprite paths, colors, positions, scales)

## Step 6: Update references (if replacing an Options asset)

If the original prefab was referenced via a ScriptableObject (like `CardFactoryOptions`):

1. Replace the options class injection with `Prefabs.PropertyName.As<ComponentType>()`
2. Remove the options registration from the DI extensions (e.g., `.WithAsset<MyOptions>()`)
3. Delete the options `.cs` file and its `.asset` + `.meta` files
4. Verify compilation

## Important Notes

- Stale prefab cleanup is automatic: `PrefabGenerator` deletes any `.prefab` in `Generated/` that is not produced by a current `[PrefabDefinition]`. No manual cleanup needed when renaming or removing definitions.
- MUST wrap definitions in `#if UNITY_EDITOR` / `#endif` — PrefabBuilder is editor-only.

## Checklist

- [ ] MCP hierarchy analysis complete (all objects, all components)
- [ ] Component properties captured (sprites, colors, positions, scales, sorting)
- [ ] Custom component source files read (field names and types)
- [ ] Cross-references mapped (which component references which)
- [ ] `[PrefabDefinition]` class created with correct structure
- [ ] Compilation passes (no errors in Unity console)
- [ ] Generated prefab hierarchy matches original
- [ ] Component properties match original (spot-check key values)
- [ ] Old Options class removed (if applicable)
- [ ] DI registration updated to use `Prefabs.X` (if applicable)
