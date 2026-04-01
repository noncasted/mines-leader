# Board Mechanics Tests

Unit tests (no Orleans). Core board logic: generation, reveal, scanner, cells, patterns.

## Done

### Reveal (flood-fill) — `Game/RevealTests.cs` (11 tests)
- [x] Empty board — opens everything
- [x] Single mine — stops at border
- [x] Mine ring — contains flood-fill
- [x] Corridor between mines — adjacent cells stay Taken
- [x] Click on mine-adjacent cell — limited expansion
- [x] Click at corner — expands with mine pockets
- [x] Dense mine field — minimal expansion
- [x] Two regions separated by mine wall — only clicked side opens
- [x] Flagged cell without mine — reveal ignores flag
- [x] Mines along edge — fills below mine row
- [x] Diagonal mines — dense blocking

### Board Generation — `Game/BoardGenerationTests.cs` (7 tests)
- [x] Creates Size x Size cells, all Taken
- [x] Places exactly Mines count of mines
- [x] Start position is mine-free
- [x] Start neighbours are mine-free (safe zone)
- [x] Corner start — neighbours are mine-free
- [x] Mines distributed randomly (statistical test)
- [x] Max mines — fills all non-safe positions

### MinesScanner — `Game/MinesScannerTests.cs` (7 tests)
- [x] Free cell with 1 adjacent mine → MinesAround = 1
- [x] Free cell with 0 adjacent mines → MinesAround = 0
- [x] Diagonal mines counted correctly
- [x] Free cell surrounded by mines → MinesAround = 8
- [x] Recalculates after cell status change (Taken→Free)
- [x] Taken cells are not scanned
- [x] Mine in Taken cell counted by adjacent Free cell

### Cell State Transitions — `Game/CellStateTests.cs` (13 tests)
- [x] TakenCell.ToFree() → creates FreeCell, updates board
- [x] FreeCell.ToTaken() → creates TakenCell, updates board
- [x] TakenCell.ToTaken() returns self (no-op)
- [x] FreeCell.ToFree() returns self (no-op)
- [x] SetMine sets HasMine
- [x] SetFlag sets IsFlagged
- [x] RemoveFlag clears IsFlagged
- [x] Default state — no mine, no flag
- [x] AddEffect on TakenCell
- [x] RemoveEffect on TakenCell
- [x] AddEffect on FreeCell
- [x] RemoveEffect on FreeCell
- [x] ToFree updates board.Cells dictionary

### PatternShapes — `Game/PatternShapesTests.cs` (12 tests)
- [x] Rhombus(3) — cross shape, 5 cells
- [x] Rhombus(4) — even, small diamond
- [x] Rhombus(5) — larger diamond, 13 cells
- [x] Rhombus symmetry verified
- [x] SelectTaken filters only Taken cells
- [x] SelectFree filters only Free cells
- [x] Pattern clips at top-left edge
- [x] Pattern clips at bottom-right edge
- [x] Center of board — no clipping, full count
- [x] Invalid center (-1,-1) returns empty
- [x] Line horizontal shape
- [x] Line vertical shape
