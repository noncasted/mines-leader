# Pixel-Art UI Toolkit Guide

Reference for building pixel-art UI in Unity UI Toolkit for this project.
Covers canvas setup, asset pipeline, file layout, USS authoring rules, and ScrollView pixel-art fixes.

Art is native **512×288**. All numbers in UXML and USS are expressed in **art pixels** (1 unit = 1 source pixel). UI Builder and PanelSettings must agree on this.

## 1. PanelSettings

All menu screens use `client/Assets/Menu/UI/MenuPanelSettings.asset`.

Required setup:
- **Reference Resolution:** `512 × 288` (art-native).
- **Scale Mode:** `Scale With Screen Size`.
- **Screen Match:** `1` (match height; width letterboxes on non-16:9).
- **Theme Style Sheet:** `UnityDefaultRuntimeTheme.tss` in the same folder.

**UI Builder Canvas:** When opening any `.uxml`, the Canvas panel's Reference Resolution must also be **512 × 288**. If it defaults to Full HD, everything will look microscopic. This is a common source of "why does my UI look broken in Builder?" confusion.

Root elements must NOT hardcode `width: 1920px; height: 1080px`. Use `flex-grow: 1` on the root so PanelSettings drives the canvas size.

## 2. File Organization

Everything UI-Toolkit-related lives under `client/Assets/Menu/UI/`:

```
client/Assets/Menu/UI/
├── MenuPanelSettings.asset          # shared PanelSettings
├── UnityDefaultRuntimeTheme.tss     # theme file
├── Styles/
│   └── MenuTheme.uss                # shared tokens: colors, font utilities
├── Main/
│   ├── MenuBottomBar.uxml
│   └── MenuBottomBar.uss
├── Decks/
│   ├── MenuCards.uxml               # screen
│   ├── MenuCards.uss                # screen-specific styles
│   └── MenuCard.uxml                # card template (instantiated many times)
└── History/
    ├── MenuHistory.uxml             # match history screen
    └── MenuHistory.uss              # history-specific styles (stats row, entry list, details panel)
```

`.cs` scripts and prefabs stay in their original screen folders (`Menu/Main/`, `Menu/Decks/`). Only UXML/USS and panel assets live under `Menu/UI/`.

**Rule:** never put UXML/USS files next to code. UI authoring and runtime code are edited with different tools by different workflows.

## 3. Asset Pipeline

All sprites used by USS (`resource("…")`) live in `client/Assets/Resources/`:

```
client/Assets/Resources/
├── Cards/                           # card artwork (Bloodhound.psd, ...)
├── Menu/                            # menu UI sprites
│   ├── background_blue.psd
│   ├── card_plate.psd
│   ├── exit_button.psd
│   ├── mana_icon.psd
│   ├── slider.psd
│   └── slider_back.psd
├── BITACH.TTF                       # pixel font
├── DreiFraktur.ttf
├── Ithaca-LVB75.ttf
└── NavButtonBackdrop.png
```

### Texture import settings for pixel art

Every sprite referenced from UI must be imported with:
- **Filter Mode:** `Point (no filter)` — no bilinear blur.
- **Compression:** `None` — avoid block artifacts.
- **Generate Mip Maps:** `off` — UI never needs mips.
- **Pixels Per Unit:** `1`.

Wrong filter mode is the #1 cause of "why are my pixels blurry?".

### `resource()` vs `url()`

**Always use `resource("Path/Name")`** — no extension, no GUID, no `project://` prefix.

```css
/* Good */
background-image: resource("Menu/card_plate");
-unity-font-definition: resource("Ithaca-LVB75");
```

```css
/* Bad — GUID path rots on rename, harder to read */
background-image: url("project://database/Assets/.../card_plate.psd?fileID=21300000&guid=...&type=3#card_plate");
```

`resource()` requires the asset be under `Assets/Resources/`. If an asset lives outside `Resources/` it must be moved before USS can reference it cleanly.

The one exception: `<Style src="...">` in UXML uses GUID URLs. Unity auto-updates these when files move, so they are safe.

## 4. USS vs Inline — When to Use Which

**UXML inline style** is the source of truth for per-element values (position, size, per-element offset). UI Builder writes inline styles when you drag values in the inspector.

**USS class** exists only when at least one of the following is true:
1. The style is reused on **2+ elements** (real reuse, not "this template is instantiated many times" — a per-template rule is still effectively inline).
2. It needs a pseudo-selector (`:hover`, `:active`, `:focus`, `.some-modifier`).
3. It needs a child/descendant selector (`.parent .child`).
4. It is a theme token (color variable, font utility).

If none of those apply, delete the class and move the rules inline. A one-off class on a single element is pure noise: it makes UI Builder slower (you must context-switch between panels), and it invites conflicts with inline overrides.

### When class and inline conflict

Inline wins. If a class rule is always overridden by inline, either:
- Remove the rule from the class, or
- Remove the class entirely.

Do not leave dead rules in the class expecting them to "apply somewhere". They don't — they mislead.

### Example: bad class

```css
/* Bad: single-use, every value is overridden by inline */
.card-info {
    position: absolute;
    left: 0;
    right: 0;
    bottom: 0;
    height: 45%;
    padding: 0 8px;
}
```

```xml
<Label name="card-info" class="card-info"
       style="height: 16px; right: 5px; bottom: 2px; left: 5px; padding: 0 2px; ..."/>
```

**Fix:** delete the class, all values live inline.

### Example: good class

```css
/* Good: modifier-driven, needs selectors */
.card-element {
    width: 42px;
    height: 48px;
    overflow: hidden;
}

.card-element.drag-hover {
    overflow: visible;
}

.card-element.drag-hover .card-glow {
    display: flex;
}
```

Modifier `.drag-hover` and child selector `.card-glow` cannot be expressed inline — class is required.

## 5. Utility Classes (Theme Tokens)

`MenuTheme.uss` holds reusable utility classes. Use them in every screen.

### Color variables

| Variable | Purpose |
|----------|---------|
| `--color-bar-upper` | Top strip of beveled bars |
| `--color-bar-divider` | Divider strip |
| `--color-bar-lower` | Bottom strip of beveled bars |
| `--color-input-bg` | Input fields, chat, panel body |
| `--color-text` | Regular text |
| `--color-text-accent` | Emphasized text |
| `--color-panel-border-outer` | Outermost beveled panel border |
| `--color-panel-border-mid` | Middle beveled panel border |
| `--color-panel-highlight` | Panel highlight strip |
| `--color-panel-header-upper` | Panel header top strip |
| `--color-panel-header-lower` | Panel header bottom strip |
| `--color-panel-bg` | Panel body fill |

Never hardcode RGB in screen USS files. If a shade is needed that doesn't exist, request a new token.

### Font utilities

| Class | Font |
|-------|------|
| `.u-ithaca` | Pixel display font (headers, button labels, card names) |

Use via `class="u-ithaca"` on any Label that needs the font.

Other fonts (DreiFraktur, BITACH) are referenced directly inline via `-unity-font-definition: resource("BITACH")` when they're used in a single place. Only promote to a utility class when there are 2+ uses.

### Reusable component classes

| Class | Purpose |
|-------|---------|
| `.panel` / `.panel-mid` / `.panel-body` | Nested beveled panel wrapper |
| `.panel-highlight` / `.panel-header-upper` / `.panel-header-lower` | Inner strip borders |
| `.settings-overlay` / `.settings-panel` / `.settings-row` | Settings dialog layout |
| `.settings-slider` | Pixel-art styled slider |
| `.settings-toggle-group` / `.settings-toggle-btn` | Toggle button group |

## 6. Sizing Strategy

### Element sizes anchored to sprites

When an element IS a sprite, set its width/height to the sprite's native dimensions (1:1 with art):

| Element | Sprite | Size |
|---------|--------|------|
| `mana-orb` | `mana_icon.psd` 11×11 | `11 × 11` |
| `exit-btn` | `exit_button.psd` 33×33 | `33 × 33` |
| `card-element` | `card_plate.psd` 42×48 | `42 × 48` |
| Scrollbar pieces | `slider_back.psd` 7×7 / `slider.psd` 7×9 | 7px wide |

### Layout sizes and positions

Everything else is calculated in art-pixels directly. Small numbers — e.g. `font-size: 17`, `top: 16`, `margin: 2` — are normal and expected. If a number looks too large (e.g., `width: 390`), it's a holdover from a Full-HD reference and needs recalculating.

### Percents

Percents (`width: 80%`, `bottom: 100%`) are allowed **inside an element with an explicit pixel size** (so the percent resolves to a known pixel count). Never put a percent on the root of a flex container whose own size is flexible.

## 7. ScrollView Pixel-Art Recipe

Unity's default ScrollView styling is designed for modern UI. For pixel-art it needs several overrides. Wire them all at once in the ScrollView's class (e.g. `.pool-scroll`):

```css
.pool-scroll {
    flex-grow: 1;
    flex-shrink: 1;
    min-height: 0;
    align-self: center;
}

/* Zero out viewport/content padding */
.pool-scroll .unity-scroll-view__content-viewport {
    padding: 0;
    margin: 0;
}

.pool-scroll .unity-scroll-view__content-container {
    flex-direction: row;
    flex-wrap: wrap;
    padding: 0;
    margin: 0;
}

/* Scrollbar column width = sprite width */
.pool-scroll .unity-scroller--vertical {
    width: 7px;
}

/* Remove Unity's default reserve-space-for-up/down-buttons margin */
.pool-scroll .unity-scroller--vertical .unity-scroller__slider {
    margin: 0 0 4px 0;
    min-width: 0;
}

/* Remove Unity's default horizontal padding around the slider input */
.pool-scroll .unity-scroller--vertical .unity-base-slider__input {
    margin: 0;
    padding: 0;
}

/* Tracker with 9-slice to preserve caps on stretch */
.pool-scroll .unity-scroller--vertical .unity-base-slider__tracker {
    position: absolute;
    top: 0; bottom: 0; left: 0;
    background-image: resource("Menu/slider_back");
    -unity-background-scale-mode: stretch-to-fill;
    -unity-slice-top: 2; -unity-slice-bottom: 2;
    -unity-slice-left: 2; -unity-slice-right: 2;
    -unity-slice-scale: 1px;
    background-color: rgba(0, 0, 0, 0);
    border-width: 0; border-radius: 0;
    width: 7px; margin: 0;
}

/* Dragger: critical to override Unity's default "left: 50% + margin-left: -N" centering */
.pool-scroll .unity-scroller--vertical .unity-base-slider__dragger {
    background-image: resource("Menu/slider");
    -unity-background-scale-mode: stretch-to-fill;
    -unity-slice-top: 3; -unity-slice-bottom: 3;
    -unity-slice-left: 2; -unity-slice-right: 2;
    -unity-slice-scale: 1px;
    background-color: rgba(0, 0, 0, 0);
    border-width: 0; border-radius: 0;
    left: 0;
    width: 7px;
    height: 21px; min-height: 21px; max-height: 21px;
    margin: 0;
}

/* Hide up/down buttons */
.pool-scroll .unity-repeat-button {
    display: none;
}
```

Set `horizontal-scroller-visibility="Hidden"` on the ScrollView in UXML if horizontal scroll is never wanted. The default `Auto` can surprise you when content overflows by a pixel.

### Gap between content and scrollbar

With `flex-wrap: wrap`, the content-container sizes itself to the widest row that fits. If the ScrollView width isn't a multiple of `card-outer-width + scrollbar-width`, you get a visual gap between the last card in a row and the scrollbar.

Formula for no gap:
```
ScrollView width = N × (card-width + horizontal-margin × 2) + scrollbar-width
```

Example: 5 cards wide, card 42 + 1px margin each side = 44 outer, scrollbar 7 → `5 × 44 + 7 = 227`. Plus a small buffer (2–5px) if sub-pixel wrapping bites: 230–232.

## 8. 9-Slice for Pixel-Art Sprites

When a small sprite (7×7, 7×9) is stretched to a large area, `stretch-to-fill` distorts the corners. Use 9-slice via USS:

```css
-unity-slice-top: 2;
-unity-slice-bottom: 2;
-unity-slice-left: 2;
-unity-slice-right: 2;
-unity-slice-scale: 1px;      /* critical: 1px keeps border crisp */
```

Slice values are pixels of the source sprite to treat as "cap". The center tile stretches; caps do not. `slice-scale: 1px` means the cap renders 1:1 without scaling — required for pixel-perfect edges.

## 9. Fonts

All fonts live in `Assets/Resources/`. To use a font:

```css
-unity-font-definition: resource("Ithaca-LVB75");
```

For SDF rendering quality, small pixel fonts (`font-size: 2–6`) work best when the font is designed as a bitmap at that size. If a font renders blurry at tiny sizes, request a dedicated bitmap font — do not scale up.

Typical sizes used in the project:
- **`17px`** — headers (Deck, Average, Mana value)
- **`6px`** — card names
- **`4–5px`** — card descriptions, card mana cost

## 10. Common Pitfalls

| Symptom | Cause | Fix |
|---------|-------|-----|
| UI microscopic in UI Builder, correct in game | Builder Canvas Reference Resolution ≠ 512×288 | Set Builder canvas to 512×288 |
| Card text blurry, jagged edges | Sprite imported with bilinear filter | Set Filter Mode to Point |
| Dragger shifted 2 px right of track | Unity default `left: 50%; margin-left: -Npx` assumes default dragger width | Add `left: 0` to dragger override |
| Scrollbar track has huge top/bottom empty space | Unity reserves margin for hidden up/down buttons | Add `margin: 0` to `.unity-scroller__slider` |
| Grid wraps to fewer columns than expected + gap to scrollbar | ScrollView width doesn't match `N × card-outer + scrollbar` | Pick width = `N × 44 + 7` for 5×44 cards |
| Horizontal scrollbar appeared | Content-container fixed wider than viewport | Remove fixed width, or hide via `horizontal-scroller-visibility="Hidden"` |
| MenuCard.uxml classes not resolving in UI Builder preview | Template UXML missing `<Style>` tags | Keep `<Style>` tags in partial templates for preview |
| Sprite path suddenly breaks after rename | Using GUID `url(project://…)` that didn't track rename | Migrate to `resource("Folder/Name")` |

## 13. Game Overlay Components

Game overlay UI lives in `client/Assets/GamePlay/UI/Overlay/` and uses the same pixel-art principles as menu UI, but with a different PanelSettings reference resolution.

### File layout

```
client/Assets/GamePlay/UI/Overlay/
├── GameOverlay.uxml          # root template, imports three sub-templates
├── GameOverlay.uss           # @import only, no direct rules
├── GamePauseButton.uxml      # pause button template
├── GamePauseButton.uss       # pause button styles
├── GameRoundButton.uxml      # round timer / skip button template
├── GameRoundButton.uss       # round button styles
├── GameCardPreview.uxml      # card info tooltip template
├── GameCardPreview.uss       # card preview styles
├── GameOverlayUI.cs          # pause button click handler
├── RoundButton.cs            # round timer update + skip click
└── CardInfoDisplayUI.cs      # card preview fade in/out
```

### Template usage

`GameOverlay.uxml` composes sub-templates via `ui:Template` + `ui:Instance`:

```xml
<UXML xmlns:ui="UnityEngine.UIElements">
    <ui:Template name="GamePauseButton" src="GamePauseButton.uxml"/>
    <ui:Template name="GameRoundButton" src="GameRoundButton.uxml"/>
    <ui:Template name="GameCardPreview" src="GameCardPreview.uxml"/
    
    <ui:Style src="GameOverlay.uss"/>
    
    <ui:VisualElement name="overlay-root" class="overlay-root">
        <ui:Instance template="GamePauseButton"/>
        <ui:Instance template="GameRoundButton"/>
        <ui:Instance template="GameCardPreview"/>
    </ui:VisualElement>
</UXML>
```

### Sprite reference pattern

All overlay sprites live in `Assets/Resources/GamePlay/UI/`:

```xml
<!-- Popup menu button (16x16) -->
<VisualElement name="pause-button" style="background-image: resource(&quot;GamePlay/UI/popup_menu_button&quot;); -unity-background-scale-mode: scale-to-fit;"/>

<!-- Step button (29x21) — first/default sprite from Step.psd -->
<VisualElement name="round-button" style="background-image: resource(&quot;GamePlay/UI/Step&quot;);">
    <Label name="round-time" style="-unity-font-definition: resource(&quot;DreiFraktur&quot;); -unity-text-align: upper-center;"/>
</VisualElement>

<!-- Card desc (38x50) — first/default sprite from card_desc.psd -->
<VisualElement name="card-preview" style="background-image: resource(&quot;GamePlay/UI/card_desc&quot;); height: 50px; width: 38px;">
    <Label name="card-name" style="-unity-font-definition: resource(&quot;BITACH&quot;); font-size: 3px; -unity-text-align: lower-center; -unity-text-auto-size: best-fit 2px 4px;"/>
    <Label name="card-description" style="-unity-font-definition: resource(&quot;Ithaca-LVB75&quot;); font-size: 4px; -unity-text-align: upper-center;"/>
</VisualElement>
```

### Key differences from menu UI

| Aspect | Menu UI | Game Overlay |
|--------|---------|--------------|
| PanelSettings resolution | 512 × 288 | 512 × 288 (same asset) |
| Element sizing | Based on sprite native size | Same — sprite-native |
| Font for headers | Ithaca-LVB75 | BITACH (card names), DreiFraktur (timer) |
| Font for body | Ithaca-LVB75 | Ithaca-LVB75 (card descriptions) |
| Background image location | Inline UXML style | Inline UXML style |
| Template composition | `ui:Template` + `ui:Instance` | Same |

### Runtime sprite swap

The round button changes sprite based on whose turn it is (own vs opponent). The initial sprite is set inline in UXML; runtime code swaps it via `StyleBackground`:

```csharp
var roundButton = root.Q<VisualElement>("round-button");

if (isOwnTurn)
    roundButton.style.backgroundImage = new StyleBackground(_ownRoundSprite);
else
    roundButton.style.backgroundImage = new StyleBackground(_opponentRoundSprite);
```

The `StyleBackground` constructor accepts a `Sprite` directly and updates the inline style at runtime without touching the UXML asset.

## 14. Pre-Commit Checklist (Extended)

- [ ] PanelSettings Reference = 512×288, Match = 1.
- [ ] Every sprite referenced from USS/UXML lives in `Assets/Resources/`.
- [ ] USS uses `resource("Name")`, not `url("project://…")` for user-defined sprites.
- [ ] All sprite imports: Filter = Point, Compression = None, Mip Maps = off.
- [ ] No `.uss` class applied to only one element without `:hover` / `:active` / modifier / child selector.
- [ ] No class rule that is fully overridden by inline — either kill the class or the inline.
- [ ] Screen `.uxml` and `.uss` live in `Assets/Menu/UI/<Screen>/`, not next to code.
- [ ] UI Builder Canvas Reference Resolution set to 512×288 when editing.
- [ ] Fonts referenced via `.u-ithaca` utility (or direct `resource(...)` if single-use).
- [ ] **Overlay sprites use `resource()` inline in UXML, not USS.**
- [ ] **Overlay element size matches sprite native dimensions.**
- [ ] **Overlay uses correct font per element (BITACH / DreiFraktur / Ithaca-LVB75).**
- [ ] **No wrapper elements created solely for background-image.**

## 12. Quick Reference — What Goes Where

| What | Where |
|------|-------|
| Screen-specific USS classes | `Menu/UI/<Screen>/<Screen>.uss` |
| Screen-specific UXML | `Menu/UI/<Screen>/<Screen>.uxml` |
| Partial templates (instantiated from code) | `Menu/UI/<Screen>/<Partial>.uxml` |
| Shared color/font tokens | `Menu/UI/Styles/MenuTheme.uss` |
| Panel rendering (beveled panels, settings dialog) | `Menu/UI/Styles/MenuTheme.uss` |
| PanelSettings, runtime theme | `Menu/UI/` (root) |
| Sprites for UI | `Assets/Resources/Menu/` or `Assets/Resources/Cards/` |
| Fonts for UI | `Assets/Resources/` (root) |
| Screen MonoBehaviours, prefabs | `Menu/<Screen>/` (original locations) |
