---
task: prefab_catalog
status: complete
phase: implementation
created: 2026-08-21
updated: 2026-08-21
total_steps: 4
completed_steps: [1, 2, 3, 4]
blocked_steps: []
---

# Prefab Catalog — spec

Authoritative contract. If this file and the code disagree, this file wins. Do not invent API that is not written here.

Repo root: `/projects/mines-leader`
Unity project: `/projects/mines-leader/client`

---

## Что я хочу

Ручной префаб, который я собрал в Editor (Cell, Card_Local, Menu_Card), должен попадать в статический реестр так же, как арт попадает в `Sprites.Cards.Trebuchet`.

Это **не** PrefabBuilder (`[PrefabDefinition]` → `Resources/Generated` → `Prefabs.GlobalCamera` через `Resources.Load`). PrefabBuilder остаётся. Это **не** `GamePrefabs : EnvAsset` с полем и drag в инспекторе.

Я ставлю чекбокс на `.prefab`, выбираю группу и корневой компонент — получаю:

```csharp
await builder.LoadPrefabGroup(Prefabs.GamePlay);

Instantiate(Prefabs.GamePlay.Cell, parent);
```

`Prefabs.GamePlay.Cell` имеет тип выбранного компонента (`CellView`). Если группу не загрузили через `LoadPrefabGroup`, getter бросает. Getter никогда не грузит.

`GamePrefabs` умирает, когда группа GamePlay покрывает те же ссылки. `BoardConstructor._cellPrefab` сам не подхватится — opt-in, миграция руками в том же таске.

---

## Цель

1. Inspector-модуль на ручных `.prefab`: Catalog / Group / Root component.
2. Генератор: `PrefabGroupAsset` + typed C# + Addressables, паттерн SpriteBuilder.
3. `builder.LoadPrefabGroup` с Retain/Release.
4. Сосуществование с PrefabBuilder: `partial class Prefabs`.
5. Миграция GamePlay-потребителей `GamePrefabs` и `BoardConstructor._cellPrefab`.

---

## Контекст

- Спрайтовый каталог: `docs/tasks/complete/sprite_catalog.md`, код в `client/Assets/Tools/Runtime/SpriteBuilder/` и `client/Assets/Tools/Editor/SpriteBuilder/`.
- PrefabBuilder: `client/Assets/Tools/Runtime/PrefabBuilder/Prefabs.cs` — плоский `StaticPrefab`, `Resources.Load("Generated/...")`.
- `GamePrefabs` (`client/Assets/GamePlay/Prefabs/GamePrefabs.cs`) регистрируется через `builder.RegisterAsset<GamePrefabs>()` в `GamePlayServicesExtensions`.
- Потребители `GamePrefabs`: `CardFactory`, `DeckView`, `StashView`, `AvatarMovesView`.
- `Cell.prefab` живёт в `client/Assets/GamePlay/Boards/Options/Cell.prefab` и инстансится из `BoardConstructor` (`SerializeField CellView _cellPrefab`), не из `GamePrefabs`.
- Префабы размазаны: `GamePlay/Prefabs/`, `GamePlay/Boards/Options/`, `GamePlay/Cards/*/Options/`, `Art/Menu/Deck/`. Источник правды — чекбокс на ассете, не папка.
- `Tools.Runtime` уже ссылается на Internal (`guid: f039cb8c565843dba6da5b3c1ccde7d6`). `LoadSpriteGroup` живёт в `Tools` (`SpriteBuilderExtensions`). Новый `LoadPrefabGroup` — туда же.
- Many matches / агентские фикстуры — **другая** задача (`agent_test_extend`). Этот таск её не трогает.

---

## Locked decisions

1. `EnsureLoaded()` only throws. It never calls Addressables, `LoadGroup`, or `Retain`.
2. Loading happens only via `builder.LoadPrefabGroup(Prefabs.GamePlay)`.
3. Each group is a generated class: `GamePlayPrefabs : PrefabGroup`.
4. `Prefabs.GamePlay` is a stable singleton handle (`new()` at domain load), not the loaded ScriptableObject.
5. Property type = component chosen in the inspector (`CellView`). If none chosen, type is `GameObject`.
6. Call site is `Instantiate(Prefabs.GamePlay.Cell, parent)` — Unity `Instantiate(T)` where `T : Component`. Do **not** invent `PrefabHandle` / runtime `.As<T>()` for catalog entries.
7. Inspector via `Editor.finishedDefaultHeaderGUI`. Do **not** replace the prefab inspector with `[CustomEditor]`.
8. One prefab asset = one property. One file belongs to exactly one group.
9. Addressable unit is one `PrefabGroupAsset` per group. Individual prefabs are dependencies, not separate addressable entries. Address = asset GUID.
10. Opt-in only. Files under `Assets/Resources/Generated/` are skipped even if someone checks the box (log error, do not emit).
11. Existing PrefabBuilder `Prefabs.GlobalCamera` stays. Catalog writes `partial class Prefabs` in a **different file**. PrefabBuilder generator must emit `partial class Prefabs`.
12. `userData` JSON key is `prefabCatalog`. Merge, do not wipe other keys.
13. Changing only the root component type **does** regenerate C# (property type changed). Changing nothing must not rewrite C#.

---

## Why not a new assembly

SpriteBuilder originally wanted `Tools.SpriteBuilder` and then folded into `Tools.Runtime` to avoid `Internal → SpriteBuilder → Common.Animations → Internal`.

Prefab catalog does not mention `ISpriteAnimationData`. Runtime types live in `Tools.Runtime` next to `SpriteGroup`.

Do **not** add `Tools.Runtime → GamePlay`. Generated properties that return `CellView` would create `Tools.Runtime → GamePlay → Tools.Runtime`.

**Property type is therefore a string in metadata, but the generated C# uses the component's namespace via `using` only if that type lives in an assembly `Tools.Runtime` already references.**

If the component type is in `GamePlay` / `Menu` (almost all of them), generated code **cannot** name `CellView` inside `Tools.Runtime`.

Locked workaround (this is the real API):

```csharp
// Generated in Tools.Runtime — property type is Component or GameObject
public Component Cell { get { EnsureLoaded(); return _cell; } }

// Call site in GamePlay:
var cell = (CellView)Prefabs.GamePlay.Cell;
Instantiate(cell, parent);
```

That is ugly. Preferred locked workaround: **generated group classes live in the consumer assembly**, not in Tools.Runtime.

No. That splits generation per asmdef and is a different product.

**Actual lock — typed accessor via generic Get, plus a thin GamePlay partial:**

Too much.

**Actual lock, matching how sprites work without leaking GamePlay into Tools:**

Sprites return `Sprite` / `ISpriteAnimationData` — both are types Tools already owns.

For prefabs, Tools cannot own `CellView`. Therefore:

```csharp
public sealed class PrefabEntry {
    public GameObject Asset { get; }
    public T As<T>() where T : Component {
        var component = Asset.GetComponent<T>();
        if (component == null)
            throw new InvalidOperationException($"{Asset.name} has no {typeof(T).Name}");
        return component;
    }
}

public PrefabEntry Cell { get { EnsureLoaded(); return _cell; } }

// Call site:
Instantiate(Prefabs.GamePlay.Cell.As<CellView>(), parent);
```

Inspector still picks the **intended** root component. Generator stores the type name in the payload for validation at `LoadGroup` (GetComponent by name, log/throw if missing) and emits a comment:

```csharp
/// Intended component: GamePlay.Boards.CellView
public PrefabEntry Cell { ... }
```

Call site uses `.As<CellView>()` — this is what the user asked for. Tools.Runtime stays ignorant of GamePlay.

`LoadGroup` validates: if metadata has a component type, `GetComponent` on the prefab root must succeed; otherwise throw at load, not at first Instantiate.

---

## API

### `PrefabGroup` (handwritten)

File: `client/Assets/Tools/Runtime/PrefabCatalog/PrefabGroup.cs`

Same Retain/Release/EnsureLoaded shape as `SpriteGroup`. Copy from `SpriteGroup.cs`. Method names `LoadGroup` / `UnloadGroup`. No `Async` suffix. Extra `Release` at 0 logs and returns.

### `PrefabEntry` (handwritten)

```csharp
namespace Tools {
    public sealed class PrefabEntry {
        public PrefabEntry(GameObject asset) { Asset = asset; }

        public GameObject Asset { get; }

        public T As<T>() where T : Component {
            EnsureAsset();
            var component = Asset.GetComponent<T>();
            if (component == null)
                throw new InvalidOperationException($"{Asset.name} has no {typeof(T).Name}");
            return component;
        }

        public static implicit operator GameObject(PrefabEntry entry) => entry.Asset;
    }
}
```

Do not call `EnsureLoaded` inside `PrefabEntry` — the group getter already did.

### Generated catalog

File: `client/Assets/Tools/Runtime/PrefabCatalog/Generated/Prefabs.Catalog.cs`

```csharp
// Auto-generated by PrefabCatalogGenerator. Do not edit manually.
namespace Tools {
    public static partial class Prefabs {
        public static readonly GamePlayPrefabs GamePlay = new();
        public static readonly MenuPrefabs Menu = new();
    }
}
```

PrefabBuilder file `PrefabBuilder/Prefabs.cs` must become `public static partial class Prefabs`.

### Generated group class

```csharp
public sealed class GamePlayPrefabs : PrefabGroup {
    private const string Address = "<guid-of-GamePlay-PrefabGroupAsset>";

    private PrefabEntry _cell;
    private AsyncOperationHandle<PrefabGroupAsset> _handle;

    /// <summary>Intended component: GamePlay.Boards.CellView</summary>
    public PrefabEntry Cell {
        get { EnsureLoaded(); return _cell; }
    }

    protected override async UniTask LoadGroup() {
        _handle = Addressables.LoadAssetAsync<PrefabGroupAsset>(Address);
        var asset = await _handle.ToUniTask();
        _cell = asset.Get("Cell");
    }

    protected override void UnloadGroup() {
        _cell = null;
        if (_handle.IsValid())
            Addressables.Release(_handle);
    }
}
```

Property names: same sanitize as sprites (`SpriteCatalogMetadata.ToGroupName` / letters+digits, `G` prefix if digit). File `Cell.prefab` → `Cell`. `Card_Local.prefab` → `CardLocal`.

Class name: `{Sanitize(groupName)}Prefabs`. Label `GamePlay` → `GamePlayPrefabs`.

### Payload

```csharp
namespace Tools {
    public sealed class PrefabGroupAsset : ScriptableObject {
        [SerializeField] private PrefabAssetEntry[] _entries;

        public PrefabEntry Get(string name);
    }

    [Serializable]
    public sealed class PrefabAssetEntry {
        public string Name;
        public GameObject Prefab;
        public string ComponentType; // AssemblyQualifiedName or empty
    }
}
```

- `Get`: find entry, null prefab → throw. If `ComponentType` is set, `GetComponent` on root must exist or throw. Return `new PrefabEntry(prefab)`.
- Asset path: `Assets/Tools/Runtime/PrefabCatalog/Groups/{GroupName}.asset`
- Addressables: group `Prefabs_{GroupName}`, Pack Together. Only this asset is marked addressable. Address = GUID.

### Scope extension

File: `client/Assets/Tools/Runtime/PrefabCatalog/PrefabBuilderExtensions.cs`

Name clash with PrefabBuilder folder — call the file `PrefabCatalogExtensions.cs`.

```csharp
namespace Tools {
    public static class PrefabCatalogExtensions {
        public static IScopeBuilder LoadPrefabGroup(this IScopeBuilder builder, PrefabGroup group) {
            builder.Events.AddBeforeBuild(group.Retain);
            builder.Events.AddBeforeDispose(() => {
                group.Release();
                return UniTask.CompletedTask;
            });
            return builder;
        }
    }
}
```

Copy the SpriteBuilderExtensions lifetime wiring exactly.

---

## Inspector module

Hook: `[InitializeOnLoad]` static ctor → `Editor.finishedDefaultHeaderGUI += Draw`.

Show only when **every** target is a prefab **asset** (Project window), path ends with `.prefab`, and path does **not** start with `Assets/Resources/Generated/`.

Detect prefab asset: `editor.targets` are `GameObject` with `PrefabUtility.IsPartOfPrefabAsset` **or** `PrefabImporter`. Support both. Do not show on scene instances, prefab mode stage overrides as "in-scene", or nested children — root of the prefab asset only.

Layout (IMGUI box):

```
┌ Prefab Catalog ──────────────────────┐
│ ☑ Prefab Catalog                     │
│ Group:  [ GamePlay           ▾ ] [+] │
│ Root:   [ CellView           ▾ ]     │
└──────────────────────────────────────┘
```

- `[+]` same as Sprite Catalog: text + OK/Cancel, writes `PrefabGroups.json`.
- Root dropdown = `MonoBehaviour` components on the prefab **root** (not children). Include a first item `(GameObject)` meaning no component.
- Store selected type as `componentType` = `Type.AssemblyQualifiedName` (stable enough for GetComponent check). Empty string = GameObject.
- Multi-object: mixed values show as mixed; writing applies to every target.
- Persist immediately. Do **not** call `SaveAndReimport()`. Write `.meta` via importer `userData` + `SetDirty` + `WriteImportSettingsIfDirty`. For GameObject targets, get the importer from `AssetDatabase.GetAssetPath`.
- After metadata change, debounce `~0.5s` and `PrefabCatalogGenerator.Generate()`.

### Storage (`importer.userData`)

```json
{
  "prefabCatalog": {
    "included": true,
    "group": "GamePlay",
    "componentType": "GamePlay.Boards.CellView, GamePlay"
  }
}
```

If `prefabCatalog` is missing: treat as not included. Do **not** auto-include on first generate (unlike sprites, which assigned a default group). Prefabs without the checkbox stay out of the catalog.

### Group registry

`client/Assets/Tools/Editor/PrefabCatalog/PrefabGroups.json`

```json
{ "groups": [ "GamePlay", "Menu" ] }
```

Same behaviour as `SpriteGroupsRegistry`.

---

## Generator

Mirror `SpriteGenerator` / `SpritesClassGenerator` / `SpriteAddressablesSync`.

Editor entry points:

- `[InitializeOnLoadMethod]` delayCall
- `[MenuItem("Tools/GeneratePrefabsCatalog")]` — **not** `Tools/GeneratePrefabs` (that is PrefabBuilder)

Scan: all `.prefab` under `Assets/` except `Assets/Resources/Generated/`. Include only `included == true`.

For each group: write `Groups/{Group}.asset`, mark addressable, emit C#. Skip-write if identical. Delete stale group assets and stale `*Prefabs.cs` (not `Prefabs.cs` from PrefabBuilder — catalog writes `Prefabs.Catalog.cs` plus `{Group}Prefabs.cs`).

Do not touch PrefabBuilder output except making `Prefabs` partial.

### PrefabBuilder change (required, small)

`PrefabsClassGenerator.Build` currently emits `public static class Prefabs`. Change to `public static partial class Prefabs`. If content would only change by that keyword, write it.

---

## Migration (slice 4)

After the catalog compiles and `LoadPrefabGroup` exists:

1. Opt-in these prefabs into group `GamePlay` (inspector metadata; implementer may write userData in generate-once if faster, but the checkbox must work):
   - `GamePlay/Prefabs/Card_Local.prefab` → `CardLocal`, component `CardScopeEntity`
   - `GamePlay/Prefabs/Card_Remote.prefab` → `CardRemote`, component `CardScopeEntity`
   - `GamePlay/Prefabs/StashCard.prefab` → `StashCard`
   - `GamePlay/Prefabs/DeckCard.prefab` → `DeckCard`
   - `GamePlay/Prefabs/PlayerTurnPoint.prefab` → `AvatarTurnPoint` (property name from file: `PlayerTurnPoint` unless you set the catalog name; **use file name sanitize**: `PlayerTurnPoint`)
   - `GamePlay/Boards/Options/Cell.prefab` → `Cell`, component `CellView`
2. `PvPScopeExtensions.Construct` and `GamePlayServicesExtensions`: `builder.LoadPrefabGroup(Prefabs.GamePlay)`.
3. Replace `_prefabs.CardLocal` etc. with `Prefabs.GamePlay.CardLocal.As<CardScopeEntity>()` (or the matching component).
4. `BoardConstructor.Build` uses `Prefabs.GamePlay.Cell.As<CellView>()` instead of `_cellPrefab`. Remove the SerializeField. Editor button that rebuilds cells must still work.
5. Delete `RegisterAsset<GamePrefabs>()`. Delete `GamePrefabs.cs` + `GamePrefabs.asset` only after every compile reference is gone.
6. Menu prefabs are **not** required in this task. Opt-in API must work for them later.

`AvatarMovesView` currently instantiates `AvatarTurnPointView` from `GamePrefabs.AvatarTurnPoint`. Catalog property follows **file name**: `PlayerTurnPoint.prefab` → `Prefabs.GamePlay.PlayerTurnPoint.As<AvatarTurnPointView>()`.

---

## Style

Follow `.agents/docs/CODE_STYLE_FULL.md` and `.agents/docs/API_DESIGN_FULL.md`:

- braces on the same line (match SpriteBuilder, not old PrefabBuilder Allman)
- no `Async` suffix on UniTask
- collections never null
- try/catch around file IO, `Debug.LogError`, do not crash the editor generate path
- generated header: `// Auto-generated by PrefabCatalogGenerator. Do not edit manually.`

Reference implementations (read them):

- `client/Assets/Tools/Editor/SpriteBuilder/SpriteGenerator.cs`
- `client/Assets/Tools/Editor/SpriteBuilder/SpritesClassGenerator.cs`
- `client/Assets/Tools/Editor/SpriteBuilder/SpriteCatalogInspector.cs`
- `client/Assets/Tools/Editor/SpriteBuilder/SpriteAddressablesSync.cs`
- `client/Assets/Tools/Runtime/SpriteBuilder/SpriteGroup.cs`
- `client/Assets/Tools/Runtime/SpriteBuilder/SpriteBuilderExtensions.cs`
- `client/Assets/Tools/Editor/PrefabBuider/PrefabsClassGenerator.cs`

List new editor scripts in `client/Tools.Editor.csproj` the same way SpriteBuilder scripts are listed.

---

## Explicitly forbidden

- Converting hand prefabs to `[PrefabDefinition]` / PrefabBuilder
- `EnsureLoaded` / getters calling Addressables or `Retain`
- `SaveAndReimport` when only catalog metadata changed
- Marking every prefab addressable individually
- Putting catalog prefabs in `Resources`
- `Tools.Runtime` referencing `GamePlay` or `Menu`
- Lazy-load on first property access
- Auto-including every `.prefab` under GamePlay/Menu
- Showing the module on scene instances
- Including `Assets/Resources/Generated/**`
- Rewriting PrefabBuilder `Prefabs.cs` except `partial`
- Migrating Menu in this task
- Pixel visual QA / agent fixtures (other task)

---

## План реализации

#### 1 Inspector + metadata
- **Статус:** [x] completed
- **Цель:** На `.prefab` в Project виден Prefab Catalog; group/root пишутся в `userData`; `[+]` добавляет группу.
- **Как:** `PrefabCatalogInspector`, `PrefabCatalogMetadata`, `PrefabGroupsRegistry`, `PrefabGroups.json`. Copy Sprite Catalog inspector behaviour.
- **Проверка:** Нет `[CustomEditor]`. Нет `SaveAndReimport`. `userData` содержит `prefabCatalog` и merge не трёт другие ключи. Generated/`Resources` префабы модуль не показывает. Multi-object mixed values.
- **Файлы:** `client/Assets/Tools/Editor/PrefabCatalog/*` [новые — `Tools.Editor.csproj`]
- **Зависит от:** —
- **Блокирует:** 2

#### 2 Runtime types + generator
- **Статус:** [x] completed
- **Цель:** `Tools/GeneratePrefabsCatalog` пишет группы, C#, addressables. `Prefabs` — `partial`.
- **Как:** `PrefabGroup`, `PrefabEntry`, `PrefabGroupAsset`, `PrefabCatalogGenerator`, `PrefabsCatalogClassGenerator`, `PrefabAddressablesSync`. PrefabBuilder `PrefabsClassGenerator` → `partial class Prefabs`.
- **Проверка:** `EnsureLoaded` throws and does not load. Identical C# is not rewritten. Only `PrefabGroupAsset` is addressable. Sanitize matches sprites. PrefabBuilder `Prefabs.GlobalCamera` still compiles.
- **Файлы:** `client/Assets/Tools/Runtime/PrefabCatalog/**` [новые], `client/Assets/Tools/Editor/PrefabCatalog/**`, `client/Assets/Tools/Editor/PrefabBuider/PrefabsClassGenerator.cs`, `client/Assets/Tools/Runtime/PrefabBuilder/Prefabs.cs`
- **Зависит от:** 1
- **Блокирует:** 3, 4

#### 3 LoadPrefabGroup + lifetime
- **Статус:** [x] completed
- **Цель:** Скоуп грузит группу через `LoadPrefabGroup`; два Retain + один Release оставляют loaded.
- **Как:** `PrefabCatalogExtensions` — copy `SpriteBuilderExtensions`.
- **Проверка:** no `Async` suffix. Getter still throws when not loaded. Extra Release at 0 logs, no throw.
- **Файлы:** `client/Assets/Tools/Runtime/PrefabCatalog/PrefabCatalogExtensions.cs`
- **Зависит от:** 2
- **Блокирует:** 4

#### 4 Migrate GamePlay consumers
- **Статус:** [x] completed
- **Цель:** `GamePrefabs` удалён; Cell/карты/колода/стеш/turn point инстансятся из `Prefabs.GamePlay.*`.
- **Как:** Opt-in metadata on the six prefabs. `LoadPrefabGroup` in PvP + GamePlay services. Replace injects. Remove `RegisterAsset<GamePrefabs>`.
- **Проверка:** Grep `GamePrefabs` — zero references. `BoardConstructor` has no `_cellPrefab` SerializeField. Play mode vs-bot still instantiates cards and a 16×16 board.
- **Файлы:** `GamePlayServicesExtensions.cs`, `PvPScopeExtensions.cs`, `CardFactory.cs`, `DeckView.cs`, `StashView.cs`, `AvatarMovesView.cs`, `BoardConstructor.cs`, `GamePrefabs.cs` (delete)
- **Зависит от:** 3
- **Блокирует:** —

---

## Ключевые файлы

| Файл | Роль |
|------|------|
| `client/Assets/Tools/Runtime/SpriteBuilder/SpriteGroup.cs` | Copy Retain/Release |
| `client/Assets/Tools/Runtime/SpriteBuilder/SpriteBuilderExtensions.cs` | Copy Load*Group |
| `client/Assets/Tools/Editor/SpriteBuilder/SpriteCatalogInspector.cs` | Copy inspector hook |
| `client/Assets/Tools/Runtime/PrefabBuilder/Prefabs.cs` | Must become `partial` |
| `client/Assets/Tools/Editor/PrefabBuider/PrefabsClassGenerator.cs` | Emit `partial` |
| `client/Assets/GamePlay/Prefabs/GamePrefabs.cs` | Delete after migration |
| `client/Assets/GamePlay/Boards/Root/BoardConstructor.cs` | Cell still on the scene |
| `client/Assets/GamePlay/Cards/Services/Factory/CardFactory.cs` | CardLocal/Remote |
| `client/Assets/Internal/Scopes/Common/Events/EventLoop.cs` | AddBeforeBuild / Dispose |

## Документация к прочтению

- `.agents/docs/CODE_STYLE_FULL.md` — generated + handwritten Tools code
- `.agents/docs/API_DESIGN_FULL.md` — UniTask, no Async suffix, lists
- `.agents/docs/PREFAB_CODEGEN.md` — do not confuse with this catalog; PrefabBuilder stays
- `.agents/docs/COMMON_CONTAINER.md` — `LoadPrefabGroup` on `IScopeBuilder`, drop `RegisterAsset<GamePrefabs>`
- `.agents/docs/CLAUDE_MISTAKES.md` — assembly cycles

## Риски

- `Prefabs` name collision: forgetting `partial` wipes either catalog groups or `GlobalCamera`.
- Typed `CellView` in Tools.Runtime would cycle assemblies — that is why properties are `PrefabEntry` + `.As<T>()` at the call site.
- `BoardConstructor` in the open scene: removing `_cellPrefab` without assigning catalog load in the same slice leaves an empty board.
- Addressables: marking the prefab itself addressable duplicates SpriteBuilder's forbidden pattern. Only the group asset is the entry.
- PrefabImporter vs GameObject inspector: if the module is hooked only to one, the checkbox never appears. Handle both.
