# Card Tests — Board-Targeting

Unit tests (no Orleans). Cards that modify the game board.
Use `TestBoardBuilder` / `BoardParser` for visual board setup and assertions.
Configs loaded from `config.cards.json` via `CardConfigs.*`.

## Done

### Bloodhound — `Game/BloodhoundTests.cs` (5 tests)
Opens Taken cells in rhombus(Size) pattern, then Reveal flood-fills.
Constructor: `(IBoard target, Config config, Payload payload)`
- [x] Opens target area and flood-fills safe region
- [x] Dense mine ring limits reveal to enclosed area
- [x] All cells in cross already Free — fails
- [x] At corner — clips to board, reveals visible area
- [x] ActionData contains target player ID

### Sonar — `Game/SonarTests.cs` (5 tests)
Auto-flags unflagged mines in rhombus(Size) pattern. No cell status change.
Constructor: `(IBoard target, Config config, Payload payload)`
- [x] Flags mines within range
- [x] Multiple mines in range — all flagged
- [x] Skips already-flagged mines
- [x] Mines outside range untouched
- [x] No mines in range — fails

### OpponentBomb — `Game/OpponentBombTests.cs` (5 tests)
Targets single cell. Mine → explode + damage. No mine → Free + reveal.
Constructor: `(IPlayer opponent, IBoard target, Payload payload)`
- [x] Mine hit — explodes, deals 1 damage
- [x] No mine — reveals large safe area
- [x] Tight mine ring — reveal contained
- [x] Free cell target — fails
- [x] Out of bounds — fails

### ErosionDozer — `Game/ErosionDozerTests.cs` (4 tests)
Opens Taken cells bordering Free (GetClosedShape), closest first, up to Size.
Constructor: `(IBoard target, Config config, Payload payload)`
- [x] Erodes from Free border, reveal expands
- [x] No Free cells — fails
- [x] Mines contain reveal within ring
- [x] Target is Free — fails (GetClosedShape returns empty)

## Todo

### Trebuchet
Closes Free cells in rhombus(Size) pattern on opponent board, places mines on alternating edges.
Constructor: `(IPlayer owner, IBoard target, Config config, Payload payload)`
- [ ] Converts Free cells to Taken in pattern
- [ ] Places mines on alternating edges
- [ ] All cells already Taken — fails
- [ ] TrebuchetBoost modifier increases size

### ZipZap
Chains through unflagged mines in search radius, converts each to Free.
Constructor: `(IPlayer owner, IBoard target, MoveSnapshot snapshot, Config config, Payload payload)`
- [ ] Finds nearest unflagged mine, chains to next
- [ ] Chain length limited by Size
- [ ] No unflagged mines — fails
- [ ] Flagged mines skipped
- [ ] TrebuchetBoost modifier increases size
- [ ] Snapshot Lock/Unlock wraps changes

### OpponentFlagErase
Removes flags from opponent board in rhombus(Size) pattern.
Constructor: `(IBoard target, Config config, Payload payload)`
- [ ] Removes flags in pattern
- [ ] No flags in range — fails
- [ ] Only flagged cells affected

### OpponentFlagReshuffle
Reshuffles flags on opponent board — removes existing, places on random non-flagged mines.
Constructor: `(IBoard target, Config config, Payload payload)`
- [ ] Removes flags, places on different mines
- [ ] No flags to reshuffle — fails

### MinefieldScout
Reveals mine locations without opening cells — shows where mines are.
Constructor: `(IBoard target, Payload payload, Config config)`
- [ ] Returns list of revealed mine positions
- [ ] No mines in range — fails

### ChainReaction
Chain reaction from target position — spawns explosions that spread.
Constructor: `(IBoard target, Payload payload, Config config)`
- [ ] Chain spreads from initial position
- [ ] MaxChain limits spread
- [ ] SpawnSize determines explosion area

### Smoke
Adds Smoke CellEffect to cells in pattern for Duration rounds.
Constructor: `(IBoard target, Payload payload, Config config, IRoundActionService roundAction)`
- [ ] Adds Smoke effect to cells
- [ ] Effect has correct duration
- [ ] Requires IRoundActionService mock

### FogOfWar
Adds Fog CellEffect to cells in pattern for Duration rounds.
Constructor: `(IBoard target, Payload payload, Config config, IRoundActionService roundAction)`
- [ ] Adds Fog effect to cells
- [ ] Effect has correct duration
- [ ] Requires IRoundActionService mock
