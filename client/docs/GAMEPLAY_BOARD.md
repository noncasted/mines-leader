# GamePlay Board System

Grid-based cell management system for the 9x9 game board with state transitions and spatial operations.

## Overview

The Board System manages a 9x9 game grid where each cell can be in one of several states. It provides neighbor calculation, coordinate conversion, flood-fill algorithms, and reactive state updates synchronized with the server.

## Module Structure

```
GamePlay/Boards/
├── Board/
│   ├── Abstract/         - IBoard interface
│   ├── Root/             - Board implementation
│   └── Construction/     - BoardConstructionData
├── Cells/
│   ├── Abstract/         - IBoardCell interface
│   ├── Root/             - Cell implementation
│   ├── States/           - ICellState implementations
│   └── Entities/         - Cell visual components
├── Selection/            - CellSelectionView
└── Factory/              - BoardFactory
```

## Core Components

### Board

| Component | Purpose |
|-----------|---------|
| `IBoard` | Main board interface with cell dictionary |
| `IBoardConstructionData` | Board configuration (size, spacing, cell prefab) |
| `BoardFactory` | Board construction from configuration |

**IBoard Interface:**

```csharp
IBoard
├── IReadOnlyDictionary<Vector2Int, IBoardCell> Cells
├── Vector2Int Size (9x9)
├── Vector2Int Origin (board position)
├── IBoardCell GetCell(Vector2Int position)
├── IReadOnlyList<IBoardCell> GetNeighbors(Vector2Int position)
├── IReadOnlyList<IBoardCell> GetConnectedGroup(Vector2Int position)
├── Vector2Int WorldToBoard(Vector3 worldPosition)
└── Vector3 BoardToWorld(Vector2Int boardPosition)
```

### Cell

| Component | Purpose |
|-----------|---------|
| `IBoardCell` | Individual cell with position and state |
| `ICellState` | State marker interface (Free/Taken) |
| `ICellFreeState` | Free/unopened cell marker |
| `ICellTakenState` | Taken/opened cell marker |
| `CellSelectionView` | Visual selection UI overlay |

**IBoardCell Interface:**

```csharp
IBoardCell
├── Vector2Int Position
├── ICellState CurrentState
├── IViewableDelegate<ICellState> StateChanged
├── void TransitionTo(ICellState newState)
├── int GetMineCount() // Adjacent mine count
├── bool IsFree()
├── bool IsTaken()
└── Transform Visual // GameObject reference
```

### Cell States

**Free State (Unopened):**
```
├─ Cell not yet revealed
├─ Shows adjacent mine count (0-8)
├─ Can be clicked to transition to Taken
└─ Network synchronized
```

**Taken State (Opened):**
```
├─ Cell content revealed
├─ Can show: Mine, Bomb, Safe
├─ Cannot transition back (usually)
└─ Network synchronized
```

## Key Features

### 1. Spatial Operations

**8-Directional Neighbors:**
```csharp
Cell(3,3).GetNeighbors() = [
    (2,2), (3,2), (4,2),
    (2,3),         (4,3),
    (2,4), (3,4), (4,4)
]
```

**Coordinate Conversion:**
```csharp
Vector3 worldPos = new Vector3(10f, 0f, 15f);
Vector2Int boardPos = board.WorldToBoard(worldPos);
// Inverse: Vector3 worldPos = board.BoardToWorld(boardPos);
```

**Flood-Fill Algorithm:**
```csharp
IReadOnlyList<IBoardCell> connected = board.GetConnectedGroup(startPos);
// Returns all cells connected to startPos (same state)
// Used for revealing safe zones
```

### 2. State Management

**State Transitions:**
```
Free → Taken (user clicks)
Taken → Free (rare, special abilities)
```

**Observable Updates:**
```csharp
cell.StateChanged.Advise(lifetime, newState =>
{
    UpdateVisuals(newState);
});
```

### 3. Network Synchronization

**Board State Property:**
```csharp
NetworkProperty<BoardState> _boardState;
// Each cell's state synchronized via SharedBoardSnapshot
```

**Update Flow:**
```
User clicks cell
  ↓
Local state transition
  ↓
BoardSnapshotHandler receives update
  ↓
All cells updated via NetworkProperty
  ↓
StateChanged event fires
  ↓
UI reflects changes
```

## Architecture

### Data Flow

```
User Input (cell click)
  ↓
CellOpenAction validation
  ↓
Local optimistic update
  ↓
SharedGameAction sent to server
  ↓
Server processes and broadcasts
  ↓
SharedBoardSnapshot received
  ↓
BoardSnapshotHandler applies mutations
  ↓
Cell state transitions
  ↓
UI updates via StateChanged event
```

### Board Construction

```csharp
IBoardConstructionData config = new BoardConstructionData
{
    Size = new Vector2Int(9, 9),
    CellSpacing = 1.5f,
    CellPrefab = cellPrefabResource,
    Origin = Vector3.zero
};

IBoard board = boardFactory.Create(config);
```

## Critical Patterns

### Pattern 1: Cell State Subscription

```csharp
public class CellVisuals : MonoBehaviour
{
    private IBoardCell _cell;

    public void Setup(IBoardCell cell, IReadOnlyLifetime lifetime)
    {
        _cell = cell;
        cell.StateChanged.Advise(lifetime, OnStateChanged);
    }

    private void OnStateChanged(ICellState newState)
    {
        if (newState is ICellFreeState)
            _spriteRenderer.sprite = _freeSprite;
        else if (newState is ICellTakenState)
            _spriteRenderer.sprite = _takenSprite;
    }
}
```

### Pattern 2: Neighbor Analysis

```csharp
public class MineCounter
{
    public int CountAdjacentMines(IBoardCell cell)
    {
        return cell.GetNeighbors()
            .Count(n => n.CurrentState is ICellTakenState && IsMineTaken(n));
    }
}
```

### Pattern 3: Flood Fill Reveal

```csharp
public async UniTask RevealConnectedSafeZone(Vector2Int startPos)
{
    var connectedCells = _board.GetConnectedGroup(startPos);

    foreach (var cell in connectedCells)
    {
        cell.TransitionTo(new CellTakenState());
        await UniTask.Delay(50); // Animation stagger
    }
}
```

### Pattern 4: World-to-Board Conversion

```csharp
public void HandleCellClick(Vector3 worldPosition)
{
    Vector2Int boardPos = _board.WorldToBoard(worldPosition);

    if (_board.Cells.TryGetValue(boardPos, out var cell))
    {
        ProcessCellClick(cell);
    }
}
```

## Dependency Injection

### Service Registration

```csharp
builder.Register<BoardFactory>();
builder.Register<CellFactory>();
builder.Register<CellSelectionView>();
builder.Register<IBoard>(lifetime =>
{
    var config = lifetime.Resolve<IBoardConstructionData>();
    var factory = lifetime.Resolve<BoardFactory>();
    return factory.Create(config);
});
```

### Lifetime Management

```
GamePlay Scope
  ↓
Board Lifetime
  ├── Cell lifetimes
  ├── StateChanged subscriptions
  └── Visual components
  ↓
Automatic cleanup on scope disposal
```

## Logging Tags

```
[Gameplay] [Board]       - Board operations (creation, updates)
[Gameplay] [Cell]        - Cell state transitions
[Gameplay] [Spatial]     - Neighbor calculations, conversions
```

## Key Files

| File | Purpose |
|------|---------|
| `Boards/Board/Abstract/IBoard.cs` | Board interface |
| `Boards/Board/Root/Board.cs` | Board implementation |
| `Boards/Board/Construction/BoardConstructionData.cs` | Configuration |
| `Boards/Cells/Abstract/IBoardCell.cs` | Cell interface |
| `Boards/Cells/Root/BoardCell.cs` | Cell implementation |
| `Boards/Cells/States/ICellState.cs` | State interfaces |
| `Boards/Selection/CellSelectionView.cs` | Selection overlay |
| `Boards/Factory/BoardFactory.cs` | Board creation |

## Integration Points

- **Gameplay:** Main game loop uses board for cell clicks and state
- **Cards:** Card actions target board cells
- **Network:** SharedBoardSnapshot synchronizes all cell states
- **Input:** Mouse/touch input converted to board coordinates
- **UI:** Cell visuals display state and mine counts

## Assembly Dependencies

**Depends on:**
- Common (Network properties, animations)
- Internal (Lifetime, DI, Reactive)
- Global (Input for click detection)
- Shared (Board domain models)

**Used by:**
- GamePlay (entire module uses Board)
- Cards (target board cells)
- Players (each player has their own board)

## Critical Rules

1. **Always use Vector2Int for positions** - Grid coordinates only
2. **State transitions trigger updates** - Never bypass TransitionTo()
3. **Lifetime required for subscriptions** - Use Advise(lifetime, handler)
4. **Neighbor calculations are cached** - Don't recalculate frequently
5. **Flood-fill respects state boundaries** - Only groups same state
6. **Coordinate conversion is bidirectional** - WorldToBoard ↔ BoardToWorld

## See Also

- **docs/GAMEPLAY.md** - Complete gameplay module overview
- **docs/GAMEPLAY.md** (Cards section) - Card targeting system
- **docs/GAMEPLAY.md** (Players section) - Player board management
- **docs/NETWORK.md** - Board state synchronization
- **docs/COMMON_REACTIVE.md** - State observation patterns
