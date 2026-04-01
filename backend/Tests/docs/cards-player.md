# Card Tests — Player-Targeting

Unit tests (no Orleans). Cards that modify player stats, hand, deck, or opponent.
Require IPlayer mock (NSubstitute) with Health, Mana, Moves, Hand, Deck, Stash, Modifiers.

## Todo

### TrebuchetAimer
Grants TrebuchetBoost modifier to owner (buffs Trebuchet/ZipZap size).
Constructor: `(IPlayer owner, Config config)`
- [ ] Adds TrebuchetBoost modifier
- [ ] Stacks with existing modifier

### Medic
Heals owner for 1 HP.
Constructor: `(IPlayer owner)`
- [ ] Heals 1 HP
- [ ] Already at max HP — still succeeds (clamped)

### Siphon
Drains mana from opponent, gives to owner.
Constructor: `(IPlayer owner, IPlayer opponent, Config config)`
- [ ] Drains DrainAmount from opponent
- [ ] Adds drained mana to owner
- [ ] Opponent has 0 mana — drains nothing

### Overclock
Grants extra moves to owner.
Constructor: `(IPlayer owner, Config config)`
- [ ] Adds ExtraMoves to current moves
- [ ] Stacks with existing moves

### GraveDigger
Takes cards from stash and adds to hand.
Constructor: `(IPlayer owner, MoveSnapshot snapshot)`
- [ ] Moves cards from stash to hand
- [ ] Empty stash — fails
- [ ] Snapshot records card additions

### Scavenger
Draws cards from deck to hand.
Constructor: `(IPlayer owner, MoveSnapshot snapshot, Config config)`
- [ ] Draws DrawCount cards from deck
- [ ] Deck has fewer cards — draws what's available
- [ ] Snapshot records card additions

### HandScramble
Replaces opponent's hand with random cards from their deck.
Constructor: `(IPlayer opponent, MoveSnapshot snapshot)`
- [ ] Removes opponent hand cards, draws new from deck
- [ ] Snapshot records removals and additions

### Lockdown
Reduces opponent's moves for Duration rounds.
Constructor: `(IPlayer opponent, Config config, IRoundActionService roundAction)`
- [ ] Reduces MovesReduction from opponent's max moves
- [ ] Duration rounds effect
- [ ] Requires IRoundActionService mock

### Purge
Removes all negative effects from owner.
Constructor: `(IPlayer owner)`
- [ ] Removes all modifiers/effects
- [ ] No effects — still succeeds
