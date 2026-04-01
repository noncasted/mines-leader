# Game Benchmarks

## Overview

Tests core game logic: board generation, cell reveal flood fill, cell state transitions, player stat mechanics (Health/Mana/Moves), and card collection systems (Deck/Hand/Stash). All run locally without distributed nodes.

## Metric

`ms` — auto-fallback to `DurationMs` (no manual `ReportMetric()` call).

---

## Benchmarks

### board-generation
- **File**: `backend/Benchmarks/Game/BoardGenerationTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Board generation algorithm. Creates 16x16 board with 40 mines, verifies mine count, safety zone around starting position, and cell bounds.
- **Distributed**: No

### board-reveal
- **File**: `backend/Benchmarks/Game/BoardRevealTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Flood fill reveal algorithm. Generates board, reveals from position (8,8), verifies revealed cells are mine-free and re-reveal is idempotent.
- **Distributed**: No

### cell-state-transitions
- **File**: `backend/Benchmarks/Game/CellStateTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Cell state machine. Tests Taken/Free transitions, mine placement, flagging, explosion events on 8x8 board.
- **Distributed**: No

### player-stats
- **File**: `backend/Benchmarks/Game/PlayerStatsTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Player stat systems. Tests Health (damage/heal with clamping), Mana (restore/use), Moves (consume with lock behavior).
- **Distributed**: No

### deck-hand-stash
- **File**: `backend/Benchmarks/Game/DeckHandStashTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Card collection mechanics. Tests Deck (init with 7 cards, draw cycling), Hand (add/remove entries), Stash (LIFO pick, collect-and-clear).
- **Distributed**: No

---

## TODO

### Throughput (ops/s)
- [ ] **card-execution** — `ICard.Use()` throughput across all card types via `CardFactory.Create()`. Measures card dispatch + board mutation cost per card type.
- [ ] **command-open-cell** — `OpenCellCommand.Execute()` throughput: safe cell path vs mine-hit path. Measures reveal + health damage + explosion event overhead.
- [ ] **command-open-multiple** — `OpenMultipleCellsCommand.Execute()` chord-open: flag counting + multi-neighbour reveal. Measures batch cell processing.
- [ ] **command-set-flag** — `SetFlagAction.Execute()` + `RemoveFlagAction.Execute()` throughput.
- [ ] **board-mines-scanner** — `BoardMinesScanner.Recalculate()` throughput at different board sizes (10x10, 16x16, 30x30). Hot path called on every cell open and card use.
- [ ] **modifier-ops** — `IModifiers.Set()`, `Inc()`, `Reset()`, `Get()` read/write/increment cycle throughput.

### Correctness (ms)
- [ ] **card-use-command-pipeline** — Full `CardUseCommand.Execute()` pipeline: mana check, card create, card use, stash write, move consumption, snapshot record.
- [ ] **round-full-cycle** — `IGameRound.Process()` full round loop in TimeLimitedRound: turn switch, card restore, round action tick. Measures one complete round.
- [ ] **round-action-scheduling** — `RoundActionService.Schedule()` + `Tick()` with 50/100 scheduled actions at varying round depths. Measures scheduling accuracy.
- [ ] **bot-full-turn** — `BotRunner.OnBotTurn()` full bot turn: flag placement + cell opens + card evaluation + card use. Measures AI decision time.
- [ ] **bot-board-scan** — `BotBoardUtils.FindRandomClosedCell()`, `FindClosestUnflaggedMine()` scan cost at small (10x10) vs large (30x30) boards.
- [ ] **execution-queue-burst** — `ExecutionQueue.Enqueue()` throughput under burst load (100 concurrent commands). Measures ActionBlock serialisation overhead.
- [ ] **player-actions-fanout** — `IPlayerActions.OnCellOpened()` / `OnCardUsed()` event fan-out with 10/50/100 subscribers. Measures EventSource dispatch cost.
