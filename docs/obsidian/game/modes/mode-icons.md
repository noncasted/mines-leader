# Mode icons (PixelLab)

Square **64×64** item icons for the Play-tab mode cards. One object, simple opaque background — a flat navy tile, not a scene and not checkerboard.

## Shared settings

Use **Pixen**.

| Param | Value |
|-------|-------|
| Size | `64 × 64` |
| `no_background` | `false` |
| `outline` | `single color black outline` |
| `detail` | `medium detail` |
| `view` | `side` |

No digits, letters, card frames, UI, caverns, horizons, extra copies of the object, or particle trails.

**Style prefix:**

```
16-bit pixel art item icon, 64x64, single-color black outline, medium shading, no text, no UI. One centered object filling most of the canvas. Simple flat deep-navy background, slightly darker at the edges, no ground, no scene. Palette: deep navy, rust red, brass gold, dull steel, ember orange. Readable silhouette.
```

## Time Limited

Brass hourglass. One object.

**`description`:**

```
16-bit pixel art item icon, 64x64, single-color black outline, medium shading, no text, no UI. One centered brass hourglass filling most of the canvas. Gold metal frame, dark navy glass, upper bulb half empty, rust-red sand falling into the lower bulb, a small flame on the top cap. Simple flat deep-navy background, slightly darker at the edges, no ground, no scene, no second hourglass. Readable silhouette.
```

## Last Man Standing

Same size, same navy tile. Steel mine with a red flag.

**`description`:**

```
16-bit pixel art item icon, 64x64, single-color black outline, medium shading, no text, no UI. Same flat deep-navy background as the hourglass icon. One centered spiky naval mine filling most of the canvas, dull steel with rust-red highlights, a small red triangular minesweeper flag on the top spike, a tiny orange fuse. No ground, no scene, no extra mines. Readable silhouette.
```

## If a result is busy

Reroll tighter. Keep `64 × 64` and `no_background: false`.

- Time Limited: `64x64 pixel art icon, brass hourglass, red sand, small flame on top, flat navy background, black outline`
- Last Man Standing: `64x64 pixel art icon, steel naval mine, red triangular flag on top, flat navy background, black outline`

---

## Card frame

Empty portrait card chrome. Same layout as the in-game Trebuchet card: art window on top, name plate in the middle, parchment below. **No letters, no icon, no illustration** — Unity composites the 64×64 icon and TMP name/description on top.

### Settings

| Param | Value |
|-------|-------|
| Size | `128 × 192` (2:3) |
| `no_background` | `true` |
| `outline` | `single color black outline` |
| `detail` | `medium detail` |

**`description`:**

```
16-bit pixel art UI card, portrait 2:3, single-color black outline, medium shading, no text anywhere, no letters, no numbers, no icon, no illustration. Empty wooden trading-card frame on a transparent background. Warm brown wood border with small muted-green leaf clusters on the left and right edges. Three empty slots stacked inside: TOP a large square dark-navy well for an icon, empty, no picture; MIDDLE a thin horizontal wooden name plate, empty, no letters; BOTTOM a beige parchment text box, empty, no letters. Inner edges slightly darker. Palette: warm brown, beige parchment, deep navy well, muted green leaves, black outline. Readable at small size.
```

If PixelLab still bakes letters into the name plate or parchment, reroll shorter:

```
empty pixel art game card, brown wooden frame, green leaves on the sides, empty dark square on top, empty wood name bar, empty beige parchment below, no text, transparent background, black outline
```
