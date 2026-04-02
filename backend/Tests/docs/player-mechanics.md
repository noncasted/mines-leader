# Player Mechanics Tests

Unit tests (no Orleans). Player stats, deck, hand, stash management.
Use `ValueProperty<T>.ForTest()` for state without network sync.

## Todo

### Health — `Game/GamePlay/Players/Health.cs`
- [ ] SetMax sets maximum HP
- [ ] SetCurrent clamps to [0, Max]
- [ ] TakeDamage reduces current HP
- [ ] TakeDamage below 0 — clamps to 0
- [ ] Heal increases current HP
- [ ] Heal above max — clamps to max
- [ ] IsDead when current = 0

### Mana — `Game/GamePlay/Players/Mana.cs`
- [ ] SetMax sets maximum mana
- [ ] Restore sets current = max
- [ ] Use reduces current mana
- [ ] Use more than available — fails or clamps
- [ ] SetCurrent clamps to [0, Max]

### Moves — `Game/GamePlay/Players/Moves.cs`
- [ ] SetMax sets max moves per turn
- [ ] Restore resets to max
- [ ] OnUsed decrements by 1
- [ ] OnUsed at 0 — throws
- [ ] Lock sets to 0
- [ ] After Lock, Restore brings back to max

### Deck — `Game/GamePlay/Players/Deck.cs`
- [ ] Init fills deck with N cards cycling through selected types
- [ ] DrawCard returns a card and removes from deck
- [ ] DrawCard from empty deck — behavior
- [ ] AddCard adds to deck
- [ ] Count reflects current deck size

### Hand — `Game/GamePlay/Players/Hand.cs`
- [ ] SetSize sets hand capacity
- [ ] Add returns ActiveCard with Id and Type
- [ ] Remove by Id removes card
- [ ] Entries returns current hand

### Stash — `Game/GamePlay/Players/Stash.cs`
- [ ] Add puts card on top
- [ ] Pick returns top card (LIFO)
- [ ] Collect returns all and clears
- [ ] Count reflects stash size
- [ ] Pick from empty stash — behavior

## Todo — Round Mechanics

### TimeLimitedRound — turn timer and win conditions
- [ ] Timer countdown decrements SecondsLeft per player
- [ ] Time bonus on CellOpened action (+TimeGainPerAction)
- [ ] Time bonus on CardUsed action (+TimeGainPerAction)
- [ ] Time reaches 0 — round ends, opponent wins
- [ ] Health reaches 0 — round ends, attacker wins
- [ ] Flag winner detected after 2+ rounds
- [ ] User lifetime terminated — immediate game end
- [ ] Mana.Max increases by 1 each round
- [ ] Cards restored to Hand.Size at round start

### LastManStandingRound — move-based turns
- [ ] Turn ends when Moves.Left reaches 0
- [ ] Turn ends when timer expires (whichever first)
- [ ] TurnsCountdown polls Moves.Left every 0.2s
- [ ] Global timer (not per-player) countdown
- [ ] Mana.Max increases by 1 each round

### RoundPlayers — card restoration
- [ ] RestoreCards fills hand to Hand.Size from deck
- [ ] Deck empty — collects stash cards, re-adds to deck, then draws
- [ ] Both deck and stash empty — draws nothing
- [ ] Each card addition recorded in snapshot

### RoundActionService — delayed actions
- [ ] Schedule action with N rounds delay
- [ ] Tick decrements all scheduled action counters
- [ ] Action executes when RoundsLeft reaches 0
- [ ] Multiple actions at same round — all execute
- [ ] Schedule with 0 rounds — executes immediately on next Tick
- [ ] No scheduled actions — Tick is no-op

### PlayerActions — event tracking
- [ ] OnCellOpened fires CellOpened delegate
- [ ] OnCardUsed fires CardUsed delegate
- [ ] Actions used for time bonus calculation in TimeLimitedRound

### Modifiers — player modifier system
- [ ] Initial modifier values all 0
- [ ] Set modifier updates value and fires SyncState
- [ ] Modifier values are floats (no bounds checking)
- [ ] Modifier read returns current value

### MoveSnapshot — recording system
- [ ] RecordCardUse prepends to record list (position 0)
- [ ] RecordCardAdd appends to record list
- [ ] RecordCardRemove appends to record list
- [ ] HandleBoards subscribes to all board events
- [ ] Lock prevents recording board events
- [ ] Unlock resumes recording
- [ ] Collect returns SharedMoveSnapshot with all records
- [ ] Board records grouped by BoardOwnerId
