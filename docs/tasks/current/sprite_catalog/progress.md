# Sprite Catalog — progress

## Snapshot

| Slice | Status | Notes |
|-------|--------|-------|
| 1 Inspector + metadata | done | Editor-only inspector + userData + group registry. No codegen/runtime. |
| 2 Runtime types + generator | done | Runtime types + generator + addressables. Inspector debounce wired. |
| 3 LoadSpriteGroup + lifetime | done | Extension + Internal ref. SpriteBuilder dropped Internal/Animations refs to avoid cycle. |

## Log

### Slice 1 — Inspector + metadata

Created `client/Assets/Tools/Editor/SpriteBuilder/`:

- `SpriteCatalogInspector.cs` — `[InitializeOnLoad]` + `Editor.finishedDefaultHeaderGUI`. Shows on `Assets/Art/` png/psd/aseprite/ase importers. Kind radios, group popup, `[+]` create, Time/Color only when Kind is Animation. Multi-object mixed values. Persist via `userData` + `SetDirty` + `WriteImportSettingsIfDirty` (no `SaveAndReimport`, no `CustomEditor`).
- `SpriteCatalogMetadata.cs` — namespaced JSON `spriteCatalog` read/write. Merge keeps other `userData` keys. Defaults: folder-joined group (`Art/Game/Cells` → `GameCells`); Aseprite multi-frame → Animation, else Sheet. Sheet → Animation resets time `0.8` and color white.
- `SpriteGroupsRegistry.cs` + `SpriteGroups.json` — known group names. Dropdown is registry ∪ groups discovered under `Assets/Art`. `[+]` OK appends a sanitized name to the json and selects it.

Also listed the new scripts in `client/Tools.Editor.csproj`.

Not in this slice: `SpriteGenerator` / C# emit / addressables / runtime types. Debounce-generate after metadata edits is left for slice 2.

### Slice 2 — Runtime types + generator

Created `client/Assets/Tools/Runtime/SpriteBuilder/`:

- `Tools.SpriteBuilder.asmdef` (`guid: c8e4a1b27d3f4e6a9c5d0f8b2a147359`, `autoReferenced: true`) — refs Internal, Common.Animations, UniTask, UniTask.Addressables, Addressables, ResourceManager. Does **not** add `Tools.Runtime → Internal` or `Tools.Runtime → Common.Animations`. No cycle yet: Internal does not reference Tools.SpriteBuilder (that is slice 3).
- `SpriteGroup.cs` — `Retain`/`Release` refcount, `LoadGroup`/`UnloadGroup`, `EnsureLoaded()` throws only.
- `SpriteGroupAsset.cs` — `SpriteKind`, `SpriteEntry`, `GetSheet` / `GetAnimation`.

Created `client/Assets/Tools/Editor/SpriteBuilder/`:

- `SpriteGenerator.cs` — `[InitializeOnLoadMethod]` (delayCall) + `Tools/GenerateSprites`. Scans `Assets/Art` (`.aseprite` > `.psd` > `.png`). Assigns default `spriteCatalog` + persists userData when missing. Dedup by sanitized identifier. Writes `Groups/{Group}.asset`.
- `SpritesClassGenerator.cs` — `Generated/Sprites.cs` + `{Group}Sprites.cs`. Skip-write if identical. Time/color is asset-only so C# is not rewritten when only those change. Sanitize = letters/digits + `G` prefix if empty/digit (same keep-alnum rule as `ScenesClassGenerator`).
- `SpriteAddressablesSync.cs` — group `Sprites_{Group}`, Pack Together, address = asset GUID. Only the `SpriteGroupAsset` is marked addressable.

Changed:

- `SpriteCatalogInspector.Apply` — debounce `~0.5s` then `SpriteGenerator.Generate()`.
- `Tools.Editor.asmdef` — refs Tools.SpriteBuilder + Unity.Addressables.Editor.
- `client/Tools.Editor.csproj` — listed the new editor scripts.

Not in this slice: `LoadSpriteGroup` / Internal asmdef ref / lifetime listen. `Retain`/`Release` already live on `SpriteGroup`; slice 3 should add `Internal.SpriteBuilderExtensions` and `Internal → Tools.SpriteBuilder`. If that reverse ref is added, drop `Tools.SpriteBuilder → Internal` (unused here) or Unity will get an asmdef cycle.

`Generated/` and `Groups/` are created on editor generate (`Tools/GenerateSprites` or domain reload). Unity was not running in this environment, so those folders were not populated here.

### Slice 3 — LoadSpriteGroup + lifetime

Created:

- `client/Assets/Internal/Scopes/Services/Builder/SpriteBuilderExtensions.cs` — `builder.LoadSpriteGroup(group)` awaits `Retain()` then `builder.Lifetime.Listen(() => group.Release())`. No `Async` suffix. Lifetime listen is the only automatic unload.

Changed:

- `Internal.asmdef` — references `Tools.SpriteBuilder` (`guid: c8e4a1b27d3f4e6a9c5d0f8b2a147359`).
- `Tools.SpriteBuilder.asmdef` — dropped `Internal` and `Common.Animations`. `SpriteGroup` / `SpriteGroupAsset` do not use Internal types. Keeping both `Internal → SpriteBuilder` and `SpriteBuilder → Common.Animations` would cycle: `Internal → Tools.SpriteBuilder → Common.Animations → Internal`.
- Moved `ISpriteAnimationData.cs` + `SpriteAnimationData.cs` into `Tools.Runtime/SpriteBuilder/` (namespace `Animations` unchanged) so `GetAnimation` still returns `ISpriteAnimationData` without referencing `Common.Animations`.
- `Common.Animations.asmdef`, `Menu.asmdef`, `GamePlay.asmdef` — reference `Tools.SpriteBuilder` so existing `ISpriteAnimationData` consumers still compile.

Unchanged (already slice 2):

- `SpriteGroup.Retain` / `Release` refcount. Second `Retain` while loaded does not reload. `Release` to 0 calls `UnloadGroup` and sets `IsLoaded = false`. Extra `Release` at 0 logs and returns.
- `EnsureLoaded` still only throws.
- Generated `UnloadGroup` still nulls fields and `Addressables.Release`s the handle.

No game-scope call sites added. Load path is `builder.LoadSpriteGroup` only.
