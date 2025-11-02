# Logging in Unity

Structured logging system for the Unity client. All logs follow a unified format with tags for convenient filtering and debugging.

## Structured Logging

### Correct - Structured Parameters

```csharp
// GOOD - Using interpolated parameters for structured logging
Debug.Log($"[Gameplay] [Board] Player {playerId} placed card {cardId} at position {posX},{posY}");

// Logging result:
// [Gameplay] [Board] Player 550e8400-e29b-41d4-a716-446655440000 placed card 5 at position 3,4
```

### Incorrect - Full String Interpolation

```csharp
// BAD - Information is lost, difficult to filter and search in logs
var message = $"Player {player.Id} did {action} with {card.Name}";
Debug.Log(message);

// Result: Unstructured string, difficult to separate parameters
// Cannot filter all logs for a specific playerId
```

## Tag System

### Domain Tags (First Position)

Identifies the main functional area:

```
[Gameplay]    - Game logic (board, cards, turns, rules)
[Menu]        - UI and menus (navigation, social system)
[Meta]        - Backend integration (user, profile, matchmaking)
[Network]     - Network operations (synchronization, RPC)
[Startup]     - Initialization and loading
[Internal]    - Internal systems (DI, lifetime, scope management)
[Global]      - Global services (audio, input, camera, settings)
```

### Component Tags (Second Position)

Identifies specific component or operation:

```
[Gameplay] [Board]        - Game board and its state
[Gameplay] [Card]         - Cards and their operations
[Gameplay] [GameLoop]     - Main game loop
[Gameplay] [Turn]         - Turn management for players
[Gameplay] [Rules]        - Rules checking and validation

[Menu] [Navigation]       - Navigation between screens
[Menu] [Deck]             - Deck management
[Menu] [Matchmaking]      - Finding opponents
[Menu] [Profile]          - Profile and statistics

[Meta] [Projection]       - Backend projections (user state)
[Meta] [Authentication]   - Authentication and sessions
[Meta] [Connection]       - Server connection

[Network] [Sync]          - State synchronization
[Network] [RPC]           - Remote procedure calls
[Network] [Message]       - Message reception and transmission

[Startup] [Assembly]      - Assembly loading
[Startup] [Scene]         - Scene initialization
[Startup] [Services]      - Service registration

[Internal] [DI]           - Dependency injection
[Internal] [Lifetime]     - Lifetime management
[Internal] [Scope]        - Scope management

[Global] [Audio]          - Audio system
[Global] [Input]          - Input handling
[Global] [Camera]         - Camera management
[Global] [Settings]       - Application settings
```

### Examples from Code

```csharp
// Board state change
Debug.Log($"[Gameplay] [Board] Updated cell {x},{y} with card {cardId}");

// Server synchronization
Debug.Log($"[Network] [Sync] Received board snapshot with {cellCount} cells");

// Menu navigation
Debug.Log($"[Menu] [Navigation] Navigating to screen: {screenName}");

// Backend projection updated
Debug.Log($"[Meta] [Projection] User profile updated: level={level}, experience={exp}");

// Game logic error
Debug.LogWarning($"[Gameplay] [Rules] Invalid move by player {playerId}: {reason}");

// Critical error
Debug.LogError($"[Network] [Connection] Failed to connect to server: {error}");

// Internal systems debugging
Debug.Log($"[Internal] [DI] Registered service {typeof(T).Name} for scope {scopeId}");
```

## Log Levels

```csharp
Debug.Log()          // Normal operations, state changes, important events
Debug.LogWarning()   // Handled errors, connection issues, retries
Debug.LogError()     // Unhandled errors, exceptions
// Debug.LogDebug() - Debug only (use with #if DEVELOPMENT_BUILD conditions)
```

### When to Use Each Level

```csharp
// Debug.Log - Normal execution flow
Debug.Log("[Gameplay] [GameLoop] Turn started for player");

// Debug.LogWarning - Handled errors that the system can recover from
Debug.LogWarning("[Network] [Sync] Failed to receive update, retrying");

// Debug.LogError - Unexpected errors that require attention
Debug.LogError("[Meta] [Connection] Authentication failed: invalid token");

// Conditional logging for debugging
#if DEVELOPMENT_BUILD
Debug.Log("[Internal] [Lifetime] Creating new scope for component");
#endif
```

## Log Formatting

### Basic Format

```
[Domain] [Component] Message with {parameters}
```

### Examples of Correct Formatting

```csharp
// Well structured
Debug.Log($"[Gameplay] [Board] Cell {x},{y} updated: card={cardId}, owner={ownerPlayerId}");

// Well structured
Debug.LogWarning($"[Network] [Sync] Timeout waiting for snapshot: waited={waitTime}ms");

// Bad - no tags
Debug.Log("Player moved card");

// Bad - unclear information
Debug.Log($"[Gameplay] Error: {e}");

// Good - clear error
Debug.LogError($"[Gameplay] [Rules] Failed to validate move: {e.Message}");
```

## Critical Logging Rules

1. **Always use tags** - First `[Domain]` then `[Component]`
2. **Include IDs for filtering** - playerId, cardId, scopeId, etc.
3. **Log important state changes** - Initialization, game state changes, connection
4. **Use LogWarning for handled errors** - Not LogError
5. **Use LogError for exceptions** - Only for critical errors
6. **Don't log sensitive data** - No passwords, tokens, PII (except IDs)
7. **Use existing tag names** - Don't invent new ones
8. **Avoid logging in hot loops** - Log summaries instead

## Pattern Examples

### Pattern 1: Component Initialization

```csharp
public class GamePlayController : ISceneService
{
    public async UniTask Initialize()
    {
        Debug.Log("[Gameplay] [GameLoop] Initializing GamePlay scene");

        try
        {
            await LoadDependencies();
            await InitializeBoard();
            Debug.Log("[Gameplay] [GameLoop] Scene fully initialized");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Gameplay] [GameLoop] Failed to initialize: {ex.Message}");
            throw;
        }
    }
}
```

### Pattern 2: State Operation

```csharp
public void PlaceCard(int cardId, Vector2Int position)
{
    Debug.Log($"[Gameplay] [Board] Attempting to place card {cardId} at {position}");

    if (!ValidateMove(cardId, position))
    {
        Debug.LogWarning($"[Gameplay] [Rules] Invalid move: card={cardId}, position={position}");
        return;
    }

    _board.PlaceCard(cardId, position);
    Debug.Log($"[Gameplay] [Board] Card {cardId} placed successfully");
}
```

### Pattern 3: Server Synchronization

```csharp
public async UniTask SyncBoardState()
{
    Debug.Log("[Network] [Sync] Requesting board state from server");

    try
    {
        var snapshot = await _networkClient.GetBoardSnapshot();
        _board.ApplySnapshot(snapshot);
        Debug.Log($"[Network] [Sync] Board synced successfully: cells={snapshot.Cells.Count}");
    }
    catch (Exception ex)
    {
        Debug.LogError($"[Network] [Sync] Failed to sync: {ex.Message}");
        throw;
    }
}
```

### Pattern 4: Navigation and Screens

```csharp
public async UniTask NavigateTo(string screenName)
{
    Debug.Log($"[Menu] [Navigation] Navigating to screen: {screenName}");

    try
    {
        await _screenManager.LoadScreen(screenName);
        Debug.Log($"[Menu] [Navigation] Successfully loaded screen: {screenName}");
    }
    catch (Exception ex)
    {
        Debug.LogError($"[Menu] [Navigation] Failed to load {screenName}: {ex.Message}");
    }
}
```

## Debugging in Unity Editor

### Using Console Filter

```
[Gameplay]    - All game logic logs
[Network]     - All network operations
[Menu]        - All UI logs
[Error]       - All errors
[Warning]     - All warnings
```

### Filtering by Component

```
[Board]       - All board operations
[Sync]        - All synchronizations
[Connection]  - All connections
```

## Debugging Checklist

When solving problems:

1. **Find the first LogError** in Console
2. **Filter by tag** - Find all logs for this domain/component
3. **Check the time sequence** - What events happened before the error?
4. **Review parameter values** - What data caused the problem?
5. **Enable Development Build** for additional debugging logs

## Recommended Documentation

- **docs/OVERVIEW.md** - Understanding the architecture
- **docs/COMMON_REACTIVE.md** - Reactive logging systems
- **docs/GAMEPLAY.md** - Game logic logging
- **docs/INTERNAL.md** - Internal DI systems logging
