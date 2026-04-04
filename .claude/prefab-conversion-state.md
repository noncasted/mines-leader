# Prefab Conversion State

Coordination file for parallel prefab conversion agents.

## PrefabBuilder API (available utilities)

### Core (existing)
- `WithName(string)` — set prefab name
- `WithComponent<T>(Action<T>)` — add + configure component
- `WithComponent<T>(out T)` — add + get reference
- `WithChild(string)` — create child GO, returns GameObject
- `WithChild<T>(string)` — create child with component
- `WithChildObject(string, Action<PrefabBuilder>)` — nested builder
- `SetSerialized<T>(fieldName, value)` — set private serialized field
- `WithPosition(x,y,z)` / `WithScale(x,y,z)` / `WithRotation(x,y,z)`
- `PrefabBuilder.LoadAsset<T>(path)` — load asset by path
- `builder.GameObject` — access underlying GameObject

### NEW utilities (just added)
- `WithActive(bool)` — set activeSelf on current builder's GO
- `WithChildObject(string name, bool active, Action<PrefabBuilder>)` — create child with active state + configure
- `WithPrefabChild(string assetPath, string name = null)` — instantiate another prefab as nested child (maintains prefab link)

### RectTransform pattern (no utility needed)
```csharp
// UI components auto-add RectTransform. Configure after adding TMP/Image:
var rt = builder.GameObject.GetComponent<RectTransform>();
rt.anchoredPosition = new Vector2(x, y);
rt.sizeDelta = new Vector2(w, h);
```

### Canvas pattern
```csharp
builder.WithComponent<Canvas>(c => {
    c.renderMode = RenderMode.WorldSpace; // or ScreenSpaceOverlay
    c.sortingLayerName = "UI";
});
builder.WithComponent<CanvasScaler>(cs => {
    cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    cs.referenceResolution = new Vector2(1920, 1080);
});
builder.WithComponent<GraphicRaycaster>();
```

## Prefab assignments

| Prefab | Agent | Status | Output file |
|--------|-------|--------|-------------|
| DeckCard | agent-simple-gameplay | done | GamePlay/Cards/Deck/DeckCardPrefabDefinition.cs |
| StashCard | agent-simple-gameplay | done | GamePlay/Cards/Stash/StashCardPrefabDefinition.cs |
| PlayerTurnPoint | agent-simple-gameplay | done | GamePlay/Players/Avatars/PlayerTurnPointPrefabDefinition.cs |
| InternalScope | agent-simple-gameplay | done | Global/Setup/InternalScopePrefabDefinition.cs |
| Cell | agent-cell | pending | GamePlay/Boards/CellPrefabDefinition.cs |
| ManaPoint | agent-ui | done | GamePlay/Players/UI/Mana/ManaPointPrefabDefinition.cs |
| Mines | agent-ui | done | GamePlay/Players/UI/Mines/MinesPrefabDefinition.cs |
| Player_Local_Base | agent-players | done | GamePlay/Players/PlayerPrefabDefinitions.cs |
| Player_Remote_Base | agent-players | done | GamePlay/Players/PlayerPrefabDefinitions.cs |
| LoadingScreen | agent-global | done | Global/UI/LoadingScreens/LoadingScreenPrefabDefinition.cs |
| Settings | agent-settings | done | Global/Settings/SettingsPrefabDefinitions.cs |
| Settings_Slider | agent-settings | done | Global/Settings/SettingsPrefabDefinitions.cs |

## Prefab hierarchies (pre-analyzed)

### DeckCard (1 obj)
- DeckCard: Transform, SpriteRenderer, GamePlay.Cards.DeckCard

### StashCard (1 obj)
- StashCard: Transform, GamePlay.Cards.StashCard, SpriteRenderer

### PlayerTurnPoint (1 obj)
- PlayerTurnPoint: Transform, GamePlay.Players.AvatarTurnPointView, SpriteRenderer

### InternalScope (1 obj)
- InternalScope: Transform, Internal.InternalScope

### ManaPoint (1 obj)
- ManaPoint: RectTransform, CanvasRenderer, UI.Image, GamePlay.Players.PlayerManaPointView

### Mines (3 obj)
- Mines: RectTransform, GamePlay.Players.BoardMinesCounterView
  - Plate: RectTransform, CanvasRenderer, UI.Image
  - Count: RectTransform, CanvasRenderer, TMPro.TextMeshProUGUI

### Cell (16 obj)
- Cell: Transform, GamePlay.Boards.CellView
  - PointerHandler: Transform, GamePlay.Boards.CellPointerHandler, BoxCollider2D
  - Free (inactive): Transform, GamePlay.Boards.CellFreeView
    - Sprite: Transform, SpriteRenderer
    - Counter: RectTransform, MeshRenderer, TMPro.TextMeshPro
  - Taken: Transform, GamePlay.Boards.CellTakenView
    - Sprite: Transform, SpriteRenderer
    - Flag (inactive): Transform
      - Sprite: Transform, SpriteRenderer
  - Mine (inactive): Transform
    - Sprite: Transform, SpriteRenderer
  - Selection (inactive): Transform, GamePlay.Boards.CellSelectionView, SpriteRenderer
  - Effects: Transform, GamePlay.Boards.Effects.CellEffects
    - Smoke (inactive): Transform, SpriteRenderer, GamePlay.Boards.Effects.SmokeCellEffect
    - Fog (inactive): Transform, SpriteRenderer, GamePlay.Boards.Effects.FogCellEffect
  - Animations: Transform, SpriteRenderer, GamePlay.Boards.CellAnimator

### Player_Local_Base (16 obj)
- Player_Local_Base: Transform, GamePlay.Players.GamePlayerScope, GamePlay.Players.GamePlayerEntityView
  - Board: Transform, GamePlay.Boards.BoardFactory
  - Deck: Transform, GamePlay.Cards.DeckFactory, GamePlay.Cards.DeckView
  - Hand: Transform, GamePlay.Cards.HandFactory, GamePlay.Cards.HandView, GamePlay.Cards.HandPositions
    - Center: Transform
  - Canvas: RectTransform, Canvas, CanvasScaler, GraphicRaycaster
  - Stash: Transform, GamePlay.Cards.StashFactory, GamePlay.Cards.StashView
  - Avatar (NESTED PREFAB: Avatar.prefab): Transform, GamePlay.Players.AvatarFactory, SortingGroup
    - View: Transform, GamePlay.Players.AvatarView
      - Image: Transform, SpriteRenderer
      - Outline: Transform, SpriteRenderer
      - Mana: Transform, SpriteRenderer
        - Text: RectTransform, MeshRenderer, TMPro.TextMeshPro
      - Health: Transform, SpriteRenderer
        - Text: RectTransform, MeshRenderer, TMPro.TextMeshPro
      - Turns: Transform, GamePlay.Players.AvatarMovesView

### Player_Remote_Base (20 obj)
- Player_Remote_Base: Transform, GamePlay.Players.GamePlayerScope, GamePlay.Players.GamePlayerEntityView
  - Board: Transform, GamePlay.Boards.BoardFactory
  - Deck: Transform, GamePlay.Cards.DeckFactory, GamePlay.Cards.DeckView
    - SpawnPoint: Transform
  - Hand: Transform, GamePlay.Cards.HandFactory, GamePlay.Cards.HandView, GamePlay.Cards.HandPositions
    - Center: Transform
  - Canvas: RectTransform, Canvas, CanvasScaler, GraphicRaycaster
    - Mines (NESTED PREFAB: Mines.prefab): RectTransform, GamePlay.Players.BoardMinesCounterView
      - Plate: RectTransform, CanvasRenderer, UI.Image
      - Count: RectTransform, CanvasRenderer, TMPro.TextMeshProUGUI
  - Stash: Transform, GamePlay.Cards.StashFactory, GamePlay.Cards.StashView
  - Avatar (NESTED PREFAB: Avatar.prefab): Transform, GamePlay.Players.AvatarFactory, SortingGroup
    - (same subtree as Player_Local_Base)

### LoadingScreen (3 obj)
- LoadingScreen: RectTransform, Global.UI.LoadingScreen, CanvasGroup, Canvas, CanvasScaler, GraphicRaycaster
  - Background: RectTransform, CanvasRenderer, MPUIKIT.MPImage
  - Animation: RectTransform, CanvasRenderer, UI.Image, Global.UI.LoadingScreenAnimation

### Settings (48 obj) — uses MPUIKIT.MPImage, Exoa.Responsive.ResponsiveContainer, UI.Slider
- Settings: RectTransform, Canvas, CanvasScaler, GraphicRaycaster, Global.Settings.SettingsView
  - Plate: RectTransform, CanvasRenderer, UI.Image
    - Header: RectTransform, CanvasRenderer, TMPro.TextMeshProUGUI
    - Volumes: RectTransform, Exoa.Responsive.ResponsiveContainer
      - Master (NESTED: Settings_Slider.prefab): ...slider subtree...
      - Music (NESTED: Settings_Slider.prefab): ...
      - SFX (NESTED: Settings_Slider.prefab): ...
    - Shake (NESTED: Settings_Slider.prefab): ...
    - Vsync: RectTransform, Global.Settings.DesignGroupSelection
      - Header: ...
      - Bottom: RectTransform, ResponsiveContainer
        - On: RectTransform, CanvasRenderer, UI.Image, UI.Button, DesignButton, DesignElement
          - Text: RectTransform, CanvasRenderer, TMPro.TextMeshProUGUI, DesignElementTextColor
        - Off: same as On
    - Bottom: RectTransform, ResponsiveContainer
      - Cancel: same button pattern
      - Apply: same button pattern

### Settings_Slider (nested in Settings, used 4 times)
- Root: RectTransform
  - Header: RectTransform, CanvasRenderer, TMPro.TextMeshProUGUI
  - Slider: RectTransform, UI.Slider
    - Background: RectTransform, CanvasRenderer, MPUIKIT.MPImage
    - Fill Area: RectTransform, UI.RectMask2D
      - Fill: RectTransform, CanvasRenderer, MPUIKIT.MPImage
    - Handle Slide Area: RectTransform
      - Handle: RectTransform, CanvasRenderer, UI.Image

## Important notes for agents

1. Place [PrefabDefinition] class near related code (in same directory or Extensions file)
2. MUST wrap in #if UNITY_EDITOR / #endif — PrefabBuilder is editor-only
3. Read source .cs files of custom components to find [SerializeField] field names for SetSerialized
4. Use MCP to get exact property values: open_prefab_stage -> find_gameobjects -> read component resources -> close_prefab_stage
5. For nested prefabs, use `WithPrefabChild("Assets/Resources/Generated/PrefabName.prefab")`
6. CanvasRenderer is auto-added by UI components — don't add manually
7. Cross-references: use closure capture pattern (not out params in lambdas)
8. TextMeshPro auto-adds RectTransform — configure it after adding TMP
