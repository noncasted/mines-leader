# 0010. Board under the pointer by coordinates, not by a scan of the cells

Status: accepted (2026-11-25, commit `8c5185a5`). Full measurement (Russian):
`docs/obsidian/architecture/client-performance.md`.

## Problem

While a player holds a card over the board, two classes work every frame: `CellsSelection` (the cell under the
pointer) and `CardDropArea` (the highlight of the card's shape). They computed the bounds of the board by iterating all
256 cells four times a frame, tested every cell of both boards against the pointer, and built a new list (and for
ZipZap a new shape) for the drop data each frame.

## Decision

- The board layout is computed once in `BoardConstructionData` (bounds and cell positions in the local space of the
  board); a world point goes through `IBoard.WorldToLocal`, so moving the board does not stale the bounds.
- The cell under the pointer comes from its coordinates: `TryGetCellAt` = `WorldToBoardPosition` + `Cells[position]`.
  The per-cell hit area `CellPointerHandler` matched the cell rectangle exactly and was removed.
- `GetDropData(pointer, buffer)` fills a buffer of the caller and runs only when the cell under the pointer changes;
  the highlight is kept in two reused sets, and the drop is re-validated on release.

## Numbers

Editor, a match against the bot in Play, the real `CellsSelection` and `CardDropArea` over the two real boards, a
synthetic pointer sweeping both boards (the cell changes about every third frame), five card shapes, 3 x 300 frames per
shape, two runs. Allocations with the profiler counter "GC Allocated In Frame" minus an idle phase. The machine was
loaded, so absolute times are noisy; the order of magnitude held in both runs.

| | Before | After |
|---|---:|---:|
| Aiming tick, median over shapes | 770-930 us | 16-19 us |
| Aiming tick, p90 (frames where the cell changes) | 1,180-1,790 us | 59-113 us |
| Allocations while aiming, median | +520-650 B/frame | +16-24 B/frame |
| Main thread frame, median | 16.7-17.2 ms | 16.7-17.5 ms (vsync at 60 fps hides it) |

Not measured: the same A/B in the WebGL player. Both builds exist, but the headless `device-lab` stand cannot reach a
match (the meta host is cut there and the game has no scripted match for the stand), and the machine was too loaded for
a clean series. A qualitative check of the function names in the debug wasm shows `GetBoardWorldBounds`,
`TryFindSelection` and `CellPointerHandler.IsInside` gone and `TryGetCellAt` present.

## Cost

- The board layout is now data computed at construction: a change of the cell size or spacing at runtime would need
  the layout recomputed (nothing does that today).
- The pointer hit area of a cell is its rectangle; a cell with a different click shape would need a hit area back.
