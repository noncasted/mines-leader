# GamePlay Card System

Comprehensive card management system including deck, hand, stash, card actions, and card context logic.

## Overview

The Card System manages all aspects of card gameplay including card entities (local and remote), deck management, hand positioning, action execution, and availability checking. It provides a dual implementation pattern distinguishing between controllable local cards and observable remote cards.

## Module Structure

```
GamePlay/Cards/
├── Entities/
│   ├── Root/
│   │   ├── LocalCard.cs       - Executable local card
│   │   └── RemoteCard.cs      - Read-only opponent card
│   ├── Definitions/           - Card properties and abilities
│   └── Views/                 - Card visual components
├── Collections/
│   ├── Hand/                  - Cards in player's hand
│   ├── Deck/                  - Remaining cards to draw
│   └── Stash/                 - Special card storage
├── Context/
│   ├── Abstract/              - ICardContext interface
│   ├── Checkers/              - Availability validation
│   └── Root/                  - Context implementation
├── Actions/                   - Card execution (use, discard)
├── Factory/                   - Card creation and instantiation
└── UI/                        - Card visual and interaction
```

## Core Components

### Card Entities

| Component | Purpose |
|-----------|---------|
| `ICard` | Base card interface with properties |
| `ILocalCard` | Executable local card with actions |
| `IRemoteCard` | Read-only opponent card view |
| `ICardDefinition` | Card metadata (name, mana, abilities) |
| `CardFactory` | Card creation and pooling |

**LocalCard Interface:**

```csharp
ILocalCard : ICard
├── CardState CurrentState
├── ICardContext Context
├── UniTask<bool> ExecuteAction(CardAction action)
├── void MarkAsUsed()
├── bool CanExecute(CardAction action)
└── IViewableDelegate<CardState> StateChanged
```

**RemoteCard Interface:**

```csharp
IRemoteCard : ICard
├── CardState CurrentState (read-only)
├── IViewableDelegate<CardState> StateChanged
├── Automatic cleanup on disconnect
└── No execution capability
```

### Card Collections

| Component | Purpose |
|-----------|---------|
| `IHand` | Cards currently in player's hand |
| `IDeck` | Remaining cards in player's deck |
| `IStash` | Special cards (permanent abilities) |

**IHand Interface:**

```csharp
IHand
├── IReadOnlyList<ICard> Cards
├── void AddCard(ICard card)
├── void RemoveCard(ICard card)
├── void RepositionCard(ICard card, Vector3 position)
├── ICard GetCardAtPosition(Vector3 worldPosition)
└── IViewableDelegate<HandState> StateChanged
```

**IDeck Interface:**

```csharp
IDeck
├── int RemainingCount
├── void AddCard(ICardDefinition definition)
├── ICard DrawCard()
├── void Shuffle()
└── IViewableDelegate<int> CountChanged
```

**IStash Interface:**

```csharp
IStash
├── IReadOnlyList<ICard> Cards
├── void AddCard(ICard card)
├── void RemoveCard(ICard card)
└── IViewableDelegate<StashState> StateChanged
```

### Card Context

| Component | Purpose |
|-----------|---------|
| `ICardContext` | Card availability logic and validation |
| `ContextChecker` | Individual availability checks |

**ICardContext Interface:**

```csharp
ICardContext
├── bool IsTurnAvailable { get; }
├── bool IsManaCostValid { get; }
├── int AvailableMovesCount { get; }
├── bool CanTargetSelf { get; }
├── bool CanTargetOpponent { get; }
├── IViewableProperty<bool> IsPlayable { get; }
└── IViewableProperty<string> UnavailableReason { get; }
```

**Context Checks:**

```csharp
enum CardAvailability
{
    Playable,           // Can be used
    NotYourTurn,        // Waiting for turn
    InsufficientMana,   // Mana cost too high
    NoTargets,          // No valid target cells
    HandFull,           // Hand is full
    GameState,          // Game not in Active state
    CustomRestriction   // Card-specific rule
}
```

## Card Lifecycle

### Creation Phase

```
1. Card defined in CardDefinition
2. Added to player's deck
3. Deck shuffled before game starts
```

### Draw Phase

```
1. Player draws from deck
2. Card added to hand
3. Card context created
4. Visual representation instantiated
```

### Play Phase

```
1. Player selects card
2. Context validates availability
3. Player targets board cell
4. Card action executed (optimistic)
5. Server request sent (SharedGameAction)
```

### Resolution Phase

```
1. Server validates action
2. Action effects applied
3. SharedMoveSnapshot received
4. CardActionSnapshotHandler applies mutations
5. Card removed from hand
6. Player receives feedback
```

### End Phase

```
1. Card discarded or archived
2. Card lifetime terminated
3. Automatic cleanup via lifetime
```

## Dual Implementation Pattern

### LocalCard (Controllable)

```csharp
public class LocalCard : ILocalCard
{
    // Can execute actions
    public async UniTask<bool> ExecuteAction(CardAction action)
    {
        if (!context.IsPlayable.Value)
            return false;

        // Optimistic update
        ApplyActionLocally(action);

        // Server request
        await gameConnection.SendCardAction(action);

        return true;
    }

    // Responds to input
    public void OnSelected() { /* show options */ }

    // Tracks usage
    public void MarkAsUsed() { _state = CardState.Used; }
}
```

### RemoteCard (Observable)

```csharp
public class RemoteCard : IRemoteCard
{
    // Read-only state
    public CardState CurrentState { get; private set; }

    // Cannot execute
    public async UniTask<bool> ExecuteAction(CardAction action)
        => throw new InvalidOperationException("Cannot execute remote card");

    // Automatic cleanup
    public void OnOpponentDisconnected()
    {
        _lifetime.Terminate();
    }
}
```

## Card Context Logic

### Real-Time Validation

```csharp
cardContext.IsTurnAvailable
  ↓ (subscribes to)
gameRound.CurrentPlayer

cardContext.IsManaCostValid
  ↓ (subscribes to)
player.Mana.CurrentValue

cardContext.AvailableMovesCount
  ↓ (subscribes to)
player.Moves.CurrentValue

cardContext.IsPlayable
  ↓ (computed from above)
UI enables/disables card
```

### Target Validation

```csharp
bool CanPlayCard(CardDefinition card)
{
    // Check turn
    if (!context.IsTurnAvailable)
        return false;

    // Check mana
    if (player.Mana.Current < card.ManaCost)
        return false;

    // Check available moves
    int availableMoves = player.Moves.Current - player.Moves.MaxValue;
    if (availableMoves <= 0)
        return false;

    // Check targets
    if (card.TargetType == TargetType.Self)
        return context.CanTargetSelf;

    if (card.TargetType == TargetType.Opponent)
        return context.CanTargetOpponent;

    return true;
}
```

## Snapshot Synchronization

### Card-Related Handlers

| Handler | Records Handled | Purpose |
|---------|-----------------|---------|
| `CardActionSnapshotHandler` | Card action execution | Apply card effects |
| `CardDrawSnapshotHandler` | Cards added to hand | Add card to hand |
| `CardRemoveSnapshotHandler` | Cards removed from hand | Remove card from hand |
| `CardTakeoutFromStashSnapshotHandler` | Stash interactions | Stash card management |

### Handler Flow

```
SharedMoveSnapshot received
  ↓
SnapshotReceiver enqueues records
  ↓
Type-based dispatcher
  ↓
CardActionSnapshotHandler.Handle()
  ├─ Apply card effects to board
  ├─ Update player state (mana, moves)
  └─ Remove card from hand
```

## Key Interfaces

```csharp
ICard                           // Base card interface
ILocalCard, IRemoteCard         // Dual implementations
ICardContext                    // Availability logic
ICardDefinition                 // Card metadata
IHand, IDeck, IStash            // Collections
ICardFactory                    // Card creation
```

## Dependency Injection

### Service Registration

```csharp
builder.Register<CardFactory>();
builder.Register<ICardContext>();
builder.Register<IHand>(lifetime =>
{
    var handState = lifetime.Resolve<HandState>();
    return new Hand(handState);
});
builder.Register<IDeck>(lifetime =>
{
    var deckState = lifetime.Resolve<DeckState>();
    return new Deck(deckState);
});
builder.Register<IStash>(lifetime =>
{
    var stashState = lifetime.Resolve<StashState>();
    return new Stash(stashState);
});
```

### Lifetime Management

```
GamePlay Scope
  ↓
Card Lifetime
  ├── LocalCard lifetimes
  ├── RemoteCard lifetimes
  ├── Hand/Deck/Stash lifetimes
  ├── Context subscription lifetimes
  └── Automatic cleanup on scope disposal
```

## Logging Tags

```
[Gameplay] [Cards]       - Card operations (creation, draw, use)
[Gameplay] [Hand]        - Hand management (add, remove, position)
[Gameplay] [Deck]        - Deck operations (shuffle, draw)
[Gameplay] [Stash]       - Stash interactions
[Gameplay] [Context]     - Card availability checks
```

## Critical Patterns

### Pattern 1: Card Execution

```csharp
public async UniTask PlayCard(ILocalCard card, Vector2Int targetCell)
{
    // Check availability
    if (!card.Context.IsPlayable.Value)
    {
        Debug.LogWarning($"[Gameplay] [Cards] Card not playable: {card.Context.UnavailableReason.Value}");
        return;
    }

    // Execute
    bool success = await card.ExecuteAction(new CardAction
    {
        CardId = card.Id,
        TargetCell = targetCell
    });

    if (success)
    {
        Debug.Log($"[Gameplay] [Cards] Card {card.Id} executed");
        card.MarkAsUsed();
    }
}
```

### Pattern 2: Context Subscription

```csharp
public void SetupCard(ILocalCard card, IReadOnlyLifetime lifetime)
{
    card.Context.IsPlayable.Advise(lifetime, isPlayable =>
    {
        _cardVisuals.SetEnabled(isPlayable);
        _cardButton.interactable = isPlayable;

        if (!isPlayable)
        {
            Debug.Log($"[Gameplay] [Context] Card unavailable: {card.Context.UnavailableReason.Value}");
        }
    });
}
```

### Pattern 3: Hand Management

```csharp
public void DrawCard(ICard card)
{
    _hand.AddCard(card);
    Debug.Log($"[Gameplay] [Hand] Drew card {card.Id}, hand size: {_hand.Cards.Count}");

    card.StateChanged.Advise(_lifetime, newState =>
    {
        Debug.Log($"[Gameplay] [Cards] Card {card.Id} state changed to {newState}");
    });
}
```

### Pattern 4: Deck Management

```csharp
public void InitializeDeck(IReadOnlyList<ICardDefinition> cardDefinitions)
{
    foreach (var definition in cardDefinitions)
    {
        _deck.AddCard(definition);
    }

    _deck.Shuffle();
    Debug.Log($"[Gameplay] [Deck] Deck initialized with {_deck.RemainingCount} cards");
}
```

## Key Files

| File | Purpose |
|------|---------|
| `Cards/Entities/Root/LocalCard.cs` | Local card implementation |
| `Cards/Entities/Root/RemoteCard.cs` | Remote card implementation |
| `Cards/Collections/Hand/Hand.cs` | Hand management |
| `Cards/Collections/Deck/Deck.cs` | Deck management |
| `Cards/Collections/Stash/Stash.cs` | Stash management |
| `Cards/Context/Root/CardContext.cs` | Context implementation |
| `Cards/Factory/CardFactory.cs` | Card creation |
| `Cards/Actions/CardAction.cs` | Action definitions |

## Integration Points

- **Gameplay:** Main game loop handles card plays
- **Players:** Each player has hand, deck, stash
- **Board:** Cards target board cells
- **Network:** Snapshot handlers sync card state
- **Input:** UI enables card selection and targeting
- **Animation:** Card draw/play animations

## Assembly Dependencies

**Depends on:**
- Common (Network, Animation)
- Internal (Lifetime, DI, Reactive)
- Global (Input for selection)
- Shared (Card domain models)

**Used by:**
- GamePlay (entire module uses Cards)
- Players (card collections)
- Network sync handlers

## Critical Rules

1. **LocalCard only on your side** - Use RemoteCard for opponent
2. **Always check context** - Validate before executing action
3. **Use lifetimes** - Subscribe with Advise(lifetime, handler)
4. **Snapshot sync** - Cards synchronized via snapshot handlers
5. **State is observable** - Use StateChanged for visuals
6. **Automatic cleanup** - No manual unsubscribe needed

## See Also

- **docs/GAMEPLAY.md** - Complete gameplay module overview
- **docs/GAMEPLAY_BOARD.md** - Board and cell system
- **docs/GAMEPLAY.md** (Players section) - Player aggregate
- **docs/NETWORK.md** - Card synchronization
- **docs/COMMON_REACTIVE.md** - State observation patterns
