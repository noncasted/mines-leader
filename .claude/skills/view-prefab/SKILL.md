# View Prefab Skill

Capture a screenshot and full hierarchy of a Unity prefab. Returns the image and structural data for analysis.

---

## Arguments

The user provides a prefab path or search query. Examples:
- `/view-prefab Assets/Resources/Generated/EqualNode.prefab`
- `/view-prefab EqualNode` (search by name)
- `/view-prefab **/MyPrefab.prefab` (glob pattern)

## Execution Steps

### Step 1 — Resolve prefab path

If the argument is a full path starting with `Assets/`, use it directly.

Otherwise, search for the prefab:
1. Use Glob tool with pattern `client/Assets/**/{argument}.prefab`
2. If no results, try `client/Assets/**/*{argument}*.prefab`
3. If multiple results found, list them and ask the user to pick one
4. Strip the `client/` prefix — Unity paths start with `Assets/`

### Step 2 — Capture screenshot + hierarchy (parallel)

Run these two calls in parallel:

**Call A** — Screenshot + hierarchy via PrefabScreenshot:
```
mcp__unityMCP__execute_code:
  action: execute
  code: return Tools.EditorTools.PrefabScreenshot.Capture("{prefab_path}", 1024);
```

Parse the response:
- If starts with `ERROR:` — report the error and stop
- Extract `IMAGE_PATH:` line — absolute path to the saved PNG
- Extract everything after `---HIERARCHY---` — the object tree with components and serialized field values

**Call B** — Detailed prefab info via manage_prefabs:
```
mcp__unityMCP__manage_prefabs:
  action: get_hierarchy
  prefab_path: {prefab_path}
```

### Step 3 — Read the screenshot

Use the Read tool to read the PNG file from the path extracted in Step 2A.
The image path is in `Temp/PrefabScreenshots/` under the client project root.
Claude can view images natively — the screenshot will be visible in the conversation.

### Step 4 — Present results

Output a compact structured summary. **Filter out noise** — do NOT dump every MPImage/TextMeshProUGUI serialized field. Focus on:

```
## Prefab: {name}
Path: {prefab_path}

### Screenshot
(image is displayed inline above)

### Hierarchy (compact tree)
- RootObject
  [Component1] (key_field=value)
  [Component2]
  - Child1
    [Component3] (key_field=value)

### Key Details
- Node type: {from NodeOptions._name}
- Config ID: {from NodeOptions._nodeConfigID}
- SerializeField bindings: {custom MonoBehaviour fields only}
- Connections: {in/out connection types}
```

**What to include in component details:**
- Custom MonoBehaviour fields (the ones defined in project code)
- Text content (m_text from TextMeshProUGUI)
- Colors that define the visual identity (main MPImage color, NOT all 20 MPImage fields)
- Object references between components

**What to SKIP:**
- Default/boilerplate MPImage fields (m_FillMethod, m_Maskable, m_PixelsPerUnitMultiplier, etc.)
- Default TextMeshProUGUI typography settings (m_wordWrappingRatios, m_overflowMode, etc.)
- CanvasRenderer (always present, no useful info)
- Boolean fields that are at default values

## Notes

- Screenshot saved to `{project}/Temp/PrefabScreenshots/` — cleaned on editor restart
- UI prefabs: auto-wrapped in Canvas with CanvasScaler, sized to fit the element
- 3D prefabs: perspective camera at 30 FOV, directional light, auto-framed on bounds
- Resolution: 1024x1024, 4x antialiasing
- Source: `Assets/Tools/EditorTools/PrefabScreenshot.cs`
