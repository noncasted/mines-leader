---
name: create-card
description: Create a new card for the competitive minesweeper game. Use this skill whenever the user asks to add, create, or implement a new card, card type, or card mechanic. Also use when the user mentions a card name from docs/obsidian/game/cards/ and wants it implemented. Covers all three codebases — shared models, backend mechanics + bot strategy, console config editor, and client-side action + sync.
---

# Create Card Skill

This skill guides you through adding a new card to the game across all three codebases. A card touches ~15 files across shared/, backend/, and client/ — missing any one of them causes silent failures, so follow the checklist carefully.

## Game Balance Context

These parameters are critical for card design decisions:
- **Board:** 16x16, 40 mines
- **HP:** 3 (max 1 damage per single action)
- **Moves:** 5 per turn
- **Mana:** starts at 1, grows +1/turn
- **Deck:** 10 cards, hand up to 5, discard recycles

## Card Documentation Structure

```
docs/obsidian/game/cards/
  implemented/  — cards already in the game (reference for existing mechanics)
  queue/        — cards approved and ready for implementation (PASS status)
  ideas/        — cards in concept stage (need further design)
  fail/         — 80+ rejected cards
    fail_reasons.md  — MANDATORY validation checklist for any new card
    cards_fail_*.md  — rejected cards grouped by category with detailed reasons
```

Each folder contains `*_all.md` (summary table) and per-category files:
`*_scout.md`, `*_hand.md`, `*_improve.md`, `*_cross.md`, `*_resources.md`

## Step 0: Get the Card Description and Validate

### 0.1 Find the card

If the user gave only a card name (no description), look it up in order:
1. `docs/obsidian/game/cards/queue/cards_queue_all.md` — approved cards ready for implementation
2. `docs/obsidian/game/cards/ideas/cards_ideas_all.md` — cards in concept stage
3. `docs/obsidian/game/cards/implemented/cards_implemented_all.md` — check for duplicates
4. `docs/obsidian/game/cards/fail/cards_fail_all.md` — check if already rejected (STOP if found)

If the card is from `fail/` — inform the user it was previously rejected and show the reason.

### 0.2 Validate against fail reasons (MANDATORY)

Read `docs/obsidian/game/cards/fail/fail_reasons.md` and run the **critical checklist**. If ANY answer is "yes" — STOP and report to the user. Do NOT proceed to implementation.

**Critical checks (any "yes" = reject):**
- Does the card move mines or change MinesAround?
- Does it close already-opened cells?
- Does it delete cells or change board topology?
- Can it deal >1 damage in a single action (instant kill at 3 HP)?
- Does it reveal all mines or auto-flag everything?
- Is there NO counterplay for the opponent?
- Is the effect invisible (no visual feedback)?
- Does it require real-time actions or actions during opponent's turn?
- Does it allow infinite repetition or self-copying?
- Does it reward stepping on mines?

**Warning checks (need justification):**
- Does implementation require a new complex system?
- Is there already a card with a similar effect?
- Does it destroy player progress?
- Can the effect be explained in one sentence?
- Is the outcome pure RNG with no player agency?

### 0.3 Determine card properties

From the description, determine these properties:
- **Name** (PascalCase identifier, e.g. `Lockdown`)
- **Mana cost** (remember: mana starts at 1, grows +1/turn — expensive cards are late-game only)
- **CardTarget**: `OwnBoard`, `OpponentBoard`, `Self`, or `Opponent`
- **Category**: Scout, Hand, Improve, Cross-board, or Resources (matches folder structure)
- **Has Max variant?** (most area-effect cards do, simple cards don't)
- **Has board position?** (board-targeting cards need `IBoardCardUsePayload`)
- **Has duration/temporal effect?** (needs `IRoundActionService`)
- **Config properties** beyond ManaCost (Size, Duration, custom fields)
- **Snapshot data** beyond TargetPlayer (revealed cells, spawned mines, etc.)
- **Has RNG?** (if yes — what does the player control? pure RNG is a warning sign)

Present your analysis to the user, including the fail_reasons validation result, and confirm before proceeding.

## Step 1: Choose IDs

### CardType enum value
Cards use values in increments of 100. Max variants use base + 10.
Read `shared/Domain/CardType.cs` to find the last used value and pick the next hundred.

### MemoryPack union discriminator ID
All three union interfaces (`ICardUsePayload`, `ICardConfig`, `ICardActionData`) must use the **same** discriminator ID for the new card.
Read the existing union attributes to find the next available ID. Currently used: 0-11, 13, 16-21, 23-24. Reserved/skipped: 12, 14, 15, 22.
Pick the next unused ID (likely 25+).

## Step 2: Shared Models (shared/)

Make changes in this exact order. All files are in the `Shared` namespace.

### 2.1 CardType enum
**File:** `shared/Domain/CardType.cs`

Add the new card type(s):
```csharp
NewCard = 2600,        // next hundred after last card
NewCard_Max = 2610,    // only if has Max variant
```

### 2.2 Card Config
**File:** `shared/Configs/CardConfigOptions.cs`

Three changes in this file:

**A) Add union attribute** at the top with `ICardConfig`:
```csharp
[MemoryPackUnion(25, typeof(CardConfigOptions.NewCard))]
```

**B) Add config properties** in the `CardConfigOptions` class body:
```csharp
public NewCard NewCard_Normal { get; set; } = new();
public NewCard NewCard_Max { get; set; } = new();    // only if has Max variant
```

**C) Add to `All` dictionary**:
```csharp
{ CardType.NewCard, NewCard_Normal },
{ CardType.NewCard_Max, NewCard_Max },    // only if has Max variant
```

**D) Add config class** at the bottom:
```csharp
[MemoryPackable]
public partial class NewCard : ICardConfig {
    public CardType Type { get; set; }
    public int ManaCost { get; set; } = 3;          // default from design
    public CardTarget Target => CardTarget.Self;     // from design
    // Card-specific properties:
    // public int Size { get; set; } = 4;            // for area cards
    // public int Duration { get; set; } = 2;        // for temporal cards
}
```

### 2.3 Card Use Payload
**File:** `shared/Game/Cards/ICardUsePayload.cs`

**A) Add union attribute:**
```csharp
[MemoryPackUnion(25, typeof(CardUsePayload.NewCard))]
```

**B) Add payload class** inside `CardUsePayload`:

For board-targeting cards:
```csharp
[MemoryPackable]
public partial class NewCard : IBoardCardUsePayload {
    public CardType Type { get; set; }
    public Position Position { get; set; }
}
```

For non-board cards:
```csharp
[MemoryPackable]
public partial class NewCard : ICardUsePayload {
    public CardType Type { get; set; }
}
```

### 2.4 Card Action Snapshot
**File:** `shared/Game/Snapshots/CardActionSnapshotRecord.cs`

**A) Add union attribute:**
```csharp
[MemoryPackUnion(25, typeof(CardActionSnapshot.NewCard))]
```

**B) Add snapshot class** inside `CardActionSnapshot`:
```csharp
[MemoryPackable]
public partial class NewCard : ICardActionData {
    public Guid TargetPlayer { get; set; }
    // Add extra data if the client needs it for visualization:
    // public IReadOnlyList<Position> AffectedCells { get; set; }
}
```

## Step 3: Backend Card Mechanic (backend/Game/GamePlay/)

### 3.1 Card Implementation
**File:** `backend/Game/GamePlay/Cards/NewCard.cs` (new file)

Use the appropriate pattern based on card type:

> **Snapshot records are mandatory.** Any state change inside `Use` — cell status,
> mines, flags, effects, mana, health, moves, modifiers, hand — must be paired
> with a matching `snapshot.Record*` call. `SnapshotDiffGuard` (on by default in
> tests, togglable from the Features console) throws on drift and names the exact
> field that was mutated without a record. See
> [GAMEPLAY.md §Snapshot Sync](../../../docs/db/docs/GAMEPLAY.md#snapshot-sync) and
> [CLAUDE_MISTAKES.md Lesson 7](../../../docs/db/docs/CLAUDE_MISTAKES.md#lesson-7-every-mutation-needs-an-explicit-record-call).

**Simple non-board card (like Lockdown, Medic):**
```csharp
using Shared;

namespace Game.GamePlay;

public class NewCard : ICard<CardUsePayload.NewCard> {
    public NewCard(ICardConfigs configs) {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(CardUseContext context, CardUsePayload.NewCard payload) {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.NewCard_Normal;

        // Example: heal the player and record the mutation.
        invoker.Health.Heal(config.HealAmount);
        snapshot.RecordHealthUpdate(invoker);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.NewCard {
                TargetPlayer = invoker.User.Id
            }
        };
    }
}
```

**Board-targeting card (like Sonar, Trebuchet):**
```csharp
using Shared;

namespace Game.GamePlay;

public class NewCard : ICard<CardUsePayload.NewCard> {
    public NewCard(ICardConfigs configs, IGameContext gameContext) {
        _configs = configs;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.NewCard payload) {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var target = _gameContext.GetOpponent(invoker).Board;
        target.EnsureGenerated(payload.Position);

        // Reveal: use RecordReveal — it returns the fully-opened wave and
        // records the CellFree + MinesAround pairs automatically.
        var opened = snapshot.RecordReveal(target, new[] { payload.Position });

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.NewCard {
                TargetPlayer = target.OwnerId,
                ActionCells = opened
            }
        };
    }
}
```

**Temporal effect card (like Lockdown, Smoke):**
Add `IRoundActionService` to constructor and schedule a dispose action:
```csharp
var disposeAction = new NewCardDisposeAction(/* restore state */);
_roundActionService.Schedule(disposeAction, _config.Duration);
```
Also create the `NewCardDisposeAction : IRoundAction` class in the same file.

### 3.2 CardFactory Registration
**File:** `backend/Game/GamePlay/Cards/CardFactory.cs`

Add case(s) to the `Create` switch. The constructor args depend on what the card needs:

- `owner` — the player who used the card
- `_gameContext.GetOpponent(owner)` — the opponent player
- `GetBoard(owner, payload)` — owner's board (for OwnBoard cards)
- `GetBoard(_gameContext.GetOpponent(owner), payload)` — opponent's board (for OpponentBoard cards)
- `_configs.Value.NewCard_Normal` — config
- `(CardUsePayload.NewCard)payload` — payload cast
- `snapshot` — current move snapshot (for cards that modify hand/deck)
- `_roundActionService` — for temporal effects

```csharp
CardType.NewCard => new NewCard(
    owner,
    _configs.Value.NewCard_Normal
),
CardType.NewCard_Max => new NewCard(    // only if has Max variant
    owner,
    _configs.Value.NewCard_Max
),
```

### 3.3 Bot Strategy
**File:** `backend/Game/GamePlay/Bot/CardStrategies/NewCardStrategy.cs` (new file)

```csharp
using Shared;

namespace Game.GamePlay;

public class NewCardStrategy : IBotCardStrategy {
    public NewCardStrategy(IBotContext context, IBotCommandUtils commandUtils) {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.NewCard];

    public float Evaluate(CardType type) {
        // Return 0-10 score based on game state
        // 0 = don't use, 10 = critical to use now
        return 5f;
    }

    public bool Execute(Guid cardId, CardType cardType) {
        var bot = _context.Bot;

        var payload = new CardUsePayload.NewCard {
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}
```

For board-targeting cards, also inject `BotBoardUtils` and find a position:
```csharp
public NewCardStrategy(IBotContext context, BotBoardUtils boardUtils, IBotCommandUtils commandUtils)

public bool Execute(Guid cardId, CardType cardType) {
    var position = _boardUtils.FindRandomTakenPosition(opponent: false); // or true for opponent board
    if (position == new Position(-1, -1)) return false;

    var payload = new CardUsePayload.NewCard {
        Position = position,
        Type = cardType
    };
    return _commandUtils.UseCard(_context.Bot, cardId, payload);
}
```

### 3.4 Register Bot Strategy
**File:** `backend/Game/GamePlay/Bot/BotServiceExtensions.cs`

Add to `AddBotServices()`:
```csharp
services.AddSingleton<IBotCardStrategy, NewCardStrategy>();
```

## Step 4: Console Config Editor (backend/Console/)

### 4.1 Choose or Create Editor Component

Cards group by their config structure:
- **ManaCost only** → use existing `CardConfigEditor`
- **Size + ManaCost** → use existing `CardSizeConfigEditor`
- **Custom properties** → create a new `CardNewCardConfigEditor.razor`

If creating a new editor, follow the pattern from existing editors (e.g., `CardLockdownConfigEditor.razor` or `CardSiphonConfigEditor.razor`).

### 4.2 Update Configs.razor
**File:** `backend/Console/Pages/Configs/Configs.razor`

If the card uses a custom editor, add a type check in the appropriate target section:
```razor
@if (config is CardConfigOptions.NewCard newCard)
{
    <CardNewCardConfigEditor @key="cardType" Value="newCard" Type="cardType" />
}
```

If using an existing editor (`CardConfigEditor` or `CardSizeConfigEditor`), no change needed — it's handled by the default `else` branches.

## Step 5: Client Card Action (client/Assets/GamePlay/Cards/)

### 5.1 Card Action
**File:** `client/Assets/GamePlay/Cards/Entities/Actions/CardNewCardAction.cs` (new file)

**Non-board card (like Lockdown):**
```csharp
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class CardNewCardAction : ICardAction {
        public CardNewCardAction(ICardDropDetector dropDetector) {
            _dropDetector = dropDetector;
        }

        private readonly ICardDropDetector _dropDetector;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime) {
            var isDropped = await _dropDetector.Wait(lifetime);

            return new CardActionResult() {
                IsSuccess = isDropped,
                Payload = new CardUsePayload.NewCard()
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.NewCard> {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.NewCard payload) {
                return UniTask.CompletedTask;
            }
        }
    }
}
```

**Board-targeting card (like Sonar):**
```csharp
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Meta;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardNewCardAction : ICardAction {
        public CardNewCardAction(
            ICardContext context,
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            CardConfigOptions.NewCard config) {
            _context = context;
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _config = config;
        }

        private readonly ICardContext _context;
        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly CardConfigOptions.NewCard _config;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime) {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);
            var pattern = new Pattern(_context.TargetBoard, _config.Size);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult() {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.NewCard() {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.NewCard> {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.NewCard payload) {
                return UniTask.CompletedTask;
            }
        }

        public class Pattern : ICardDropPattern {
            public Pattern(IBoard board, int size) {
                _board = board;
                _shape = PatternShapes.Rhombus(size);
            }

            private readonly IBoard _board;
            private readonly IPattenShape _shape;

            public IReadOnlyList<IBoardCell> GetDropData(Vector2Int pointer) {
                return _shape.SelectTaken(_board, pointer);
            }
        }
    }
}
```

### 5.2 Register Client Action
**File:** `client/Assets/GamePlay/Cards/Entities/States/CardStatesExtensions.cs`

**In `AddCardAction()`** — add to both switch statements:

First switch (action registration):
```csharp
CardType.NewCard => builder.Register<CardNewCardAction>(),
CardType.NewCard_Max => builder.Register<CardNewCardAction>(),  // if Max variant
```

Second switch (config parameter):
```csharp
CardType.NewCard => registration.WithParameter(configs.NewCard_Normal),
CardType.NewCard_Max => registration.WithParameter(configs.NewCard_Max),  // if Max variant
```

**In `AddCardActionSync()`** — add:
```csharp
CardType.NewCard => Sync<CardNewCardAction.Snapshot, CardActionSnapshot.NewCard>(),
CardType.NewCard_Max => Sync<CardNewCardAction.Snapshot, CardActionSnapshot.NewCard>(),  // if Max
```

### 5.3 Card Info JSON
**File:** `client/Assets/Resources/cards-info.json`

Add entry:
```json
{
    "type": "NewCard",
    "name": "New Card",
    "description": "Description of what the card does.",
    "icon": "Cards/NewCard"
}
```

If Max variant:
```json
{
    "type": "NewCard_Max",
    "name": "New Card Max",
    "description": "Enhanced version description.",
    "icon": "Cards/NewCard_Max"
}
```

## Step 6: Add to .csproj Files

New .cs files MUST be manually added to .csproj. Find the correct project file and add `<Compile Include="..." />` entries for:
- Backend card implementation file
- Backend bot strategy file
- Client card action file (if Unity uses .csproj)
- Any new console editor .razor files

Use `grep -rl "SimilarCard" *.csproj` to find the right project files.

## Verification Checklist

After completing all steps, verify:

### Shared (must all match)
- [ ] `CardType` enum has new value(s)
- [ ] `ICardConfig` has union attribute with correct ID
- [ ] `ICardUsePayload` has union attribute with **same** ID
- [ ] `ICardActionData` has union attribute with **same** ID
- [ ] `CardConfigOptions` has config class, properties, and `All` dictionary entries
- [ ] `CardUsePayload` has payload class
- [ ] `CardActionSnapshot` has snapshot class

### Backend
- [ ] Card mechanic class implements `ICard`
- [ ] `CardFactory.Create()` has case(s) for new card type(s)
- [ ] Bot strategy class implements `IBotCardStrategy`
- [ ] `BotServiceExtensions.AddBotServices()` registers the strategy
- [ ] Console config editor works for the card's properties

### Client
- [ ] `CardNewCardAction` implements `ICardAction`
- [ ] `CardNewCardAction.Snapshot` implements `ICardActionSync<T>`
- [ ] `CardStatesExtensions.AddCardAction()` has case(s) in both switches
- [ ] `CardStatesExtensions.AddCardActionSync()` has case(s)
- [ ] `cards-info.json` has entry with correct type, name, description

### Files added to .csproj
- [ ] All new .cs files registered in appropriate .csproj
