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
