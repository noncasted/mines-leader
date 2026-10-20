---
name: write-test
description: Write integration tests for backend features using the project's custom Orleans cluster test framework. Use this skill whenever the user asks to write, create, or add a test for any backend feature — grains, state, transactions, messaging, game logic, meta systems, cards, matchmaking, user flows, or any other backend functionality. Also trigger when the user says "cover this with tests", "add test coverage", or mentions testing backend code.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /write-test, write-test, напиши интеграционный тест, write integration test, integration test, тест бэкенда, backend test, backend tests, Orleans test
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Write Integration Tests

This skill writes tests for backend features using the project's custom cluster test framework in `backend/Tests/`.

## Before writing ANY test

1. **Understand what's being tested.** Read the feature code — grain interfaces, implementations, state classes, relevant services.

2. **Find similar existing tests.** Search `backend/Tests/` for tests that cover similar functionality (same domain, similar grain patterns, similar state operations). Read 1-2 of the closest matches to see how they handle setup, assertions, and cleanup for that type of feature. This step is critical — the existing tests are the ground truth for conventions and available infrastructure.

3. **Decide the test type** based on what the feature does:

| Feature type | Test pattern | Payload | Example |
|---|---|---|---|
| Pure logic (board, cells, calculations) | Synchronous, no grains | `EmptyPayload` | `CellStateTest` |
| Single grain CRUD / state | Grain calls + transactions | `EmptyPayload` | `UserDeckTest` |
| Concurrent grain operations | `RunConcurrentIterations` | `IConcurrentIterationTestPayload` | `TransactionStateTest` |
| Cross-service messaging | Multi-node Root + Node | Custom payload | `RuntimeChannelSendStressTest` |

## Test structure

Every test follows this exact structure:

```csharp
using Common.Extensions;
using Infrastructure;
// ... other imports as needed

namespace Tests;

public class {Feature}Test
{
    // Every test defines its own payload — never reference another test's payload
    [GenerateSerializer]
    public class Payload { }
    // For concurrent tests, implement IConcurrentIterationTestPayload instead:
    // [GenerateSerializer]
    // [method: SetsRequiredMembers]
    // public class Payload() : IConcurrentIterationTestPayload
    // {
    //     [Id(0)] public int Iterations { get; set; } = 10;
    //     [Id(1)] public int Concurrent { get; set; } = 3;
    // }

    public class Root : ClusterTestRoot<Payload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans, ITransactions transactions)
            : base(utils)
        {
            _orleans = orleans;
            _transactions = transactions;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;

        public override string Group => TestGroups.Game;  // Game | Meta | State | Messaging
        public override string Title => "feature-name";   // kebab-case

        protected override async Task Run(ClusterTestNodeHandle handle, Payload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            // --- Setup ---
            // Create test data, grains, users as needed

            // --- Act ---
            // Call grain methods, perform operations

            // --- Assert ---
            // Verify results using TestAssert or direct throws

            handle.Progress.SetProgress(1f);
        }
    }
}
```

## Atomic tests — one behavior per test class

Each test class verifies ONE specific behavior or scenario. If you need to test multiple aspects of a feature, create multiple test classes — one per aspect. The reasoning: when a test fails, you want to immediately know what broke without digging through a 200-line Run() method.

**Wrong** — one giant test covering everything:
```
TaskSchedulingTest (tests queue, dedup, delay, balancer priority, concurrency all in one Run())
```

**Right** — separate focused tests:
```
TaskQueueCollectTest          — enqueue + collect basic flow
TaskQueueDeduplicationTest    — same Id behavior
TaskQueueDelayTest            — delayed task scheduling
TaskBalancerPriorityTest      — execution order by priority
TaskBalancerConcurrencyTest   — concurrent execution limit
```

Each test class is small, focused, and independently runnable. When `TaskQueueDelayTest` fails, you know exactly what's broken.

Shared test helpers (mock classes, builders) that multiple tests need can live as inner classes in a shared outer class, or in a separate helper file in the same folder.

## Key conventions

### Lifetime — ALWAYS use handle.Lifetime, NEVER new Lifetime()

This is the most critical rule in tests. The `handle.Lifetime` is managed by ClusterTestRoot — it gets terminated automatically on test completion or failure, which ensures all background work (loops, subscriptions, async operations) is properly cleaned up.

**Wrong** — creates an orphan lifetime that won't be terminated on test failure:
```csharp
var lifetime = new Lifetime();
balancer.Run(lifetime);
// if test throws before lifetime.Terminate() — background loops run forever
```

**Right** — uses the test handle's lifetime, auto-cleanup guaranteed:
```csharp
balancer.Run(handle.Lifetime);
// ClusterTestRoot terminates handle.Lifetime after Run() completes (success or failure)
```

If you need a child lifetime for a sub-scope within the test, create it from the handle:
```csharp
var child = handle.Lifetime.Child();
```

### Outer class + inner Root
The outer class is `{Feature}Test`. The inner class is always `Root` and inherits `ClusterTestRoot<TPayload>`. This is not optional — the test discovery mechanism depends on it.

### Constructor injection (NOT [Inject])
Tests use Orleans-style constructor injection. Inject only what you need: `ClusterTestUtils` is always first, then `IOrleans`, `ITransactions`, `IUserFactory`, or other services.

### Group and Title
- `Group` — a constant from `TestGroups`. Check `backend/Tests/TestGroups.cs` for the current list of groups. If the feature doesn't fit any existing group, add a new one. Groups are semantic categories — choose by what the feature IS, not where the code lives. For example, task scheduling infrastructure is NOT "State" just because it lives in the Infrastructure folder.
- `Title` — kebab-case descriptive name: `"user-deck"`, `"cell-state-transitions"`, `"transactions-state"`

### Progress tracking
Report progress throughout the test so the runner can show status:
```csharp
handle.Progress.SetStatus(OperationStatus.InProgress);
handle.Progress.SetProgress(0.3f);
handle.Progress.Log("Created user, testing deck operations");
handle.Progress.SetProgress(0.7f);
// ...
handle.Progress.SetProgress(1f);
```

### Assertions
Use `TestAssert` for clear, contextual assertions:
```csharp
TestAssert.Equal(expected, actual, "deck card count");
TestAssert.NotNull(result, "grain response");
TestAssert.True(condition, "mine should be placed");
TestAssert.GreaterThan(value, 0, "xp gained");
TestAssert.Contains(item, collection, "card in deck");
```

Or direct throws for simple checks (both styles are used in the codebase):
```csharp
if (deckState.Entries.Count == 0)
    throw new Exception("No decks after initialization");
```

Prefer `TestAssert` when possible — the context parameter makes failures easier to diagnose.

### State cleanup
Always track created state so it gets deleted after the test:
```csharp
// For user-related state (tracks all user state tables)
Cleanup.TrackUser(userId);

// For match state
Cleanup.TrackMatch(matchId);

// For specific state types
Cleanup.Track<MyState>(grainId);
```

If you don't track state, it leaks into the database and can affect other tests.

### Payload types

**No parameters needed** — define an empty payload inside the test class:
```csharp
[GenerateSerializer]
public class Payload { }

public class Root : ClusterTestRoot<Payload>
```

Every test must define its own payload type. Never reference another test's payload (like `StateMigrationTest.EmptyPayload`) — tests must be self-contained.

**Concurrent test parameters** — implement `IConcurrentIterationTestPayload`:
```csharp
[GenerateSerializer]
[method: SetsRequiredMembers]
public class StartPayload() : IConcurrentIterationTestPayload
{
    [Id(0)] public int Iterations { get; set; } = 10;
    [Id(1)] public int Concurrent { get; set; } = 3;
}
```

Then use with `RunConcurrentIterations`:
```csharp
await handle.RunConcurrentIterations(payload, Process);
```

### Transactions
Wrap grain calls in transactions when the grain interface uses `[Transaction]`:
```csharp
var result = await _transactions.Run(() => grain.DoSomething());
```

For user operations, almost everything goes through transactions:
```csharp
var state = await _transactions.Run(() => user.Deck.GetState());
await _transactions.Run(() => user.Deck.Update(0, cards));
```

### Creating test users
Use `IUserFactory` for tests that need a user context:
```csharp
var userId = (await _userFactory.Create(new UserCreateOptions())).Id;
Cleanup.TrackUser(userId);
var user = _orleans.CreateUserHandle(userId);
```

### Pure logic tests (no grains)
For testing game logic directly without Orleans:
```csharp
protected override Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
{
    var options = Options.Create(new BoardOptions { Size = 8, Mines = 10 });
    var state = new ValueProperty<BoardState>(0).ForTest();
    var board = new Board(state, Guid.NewGuid(), options);

    // test logic...

    return Task.CompletedTask;  // synchronous — return completed task
}
```

Note `.ForTest()` on `ValueProperty` — it stubs the property update sender so it works outside the real DI container.

### TestParticipants for bulk grain operations
When testing the same operation across multiple grain instances:
```csharp
var participants = TestParticipants.Create(_orleans, 5);
await participants.Run<IMyGrain>(grain => grain.DoSomething());
var results = await participants.Get<int, IMyGrain>(grain => grain.GetValue());
```

## File placement

Place the test file in the subfolder matching its `Group`. Each group has a corresponding folder:
- `TestGroups.Game` → `backend/Tests/Game/`
- `TestGroups.Meta` → `backend/Tests/Meta/`
- `TestGroups.State` → `backend/Tests/State/`
- `TestGroups.Messaging` → `backend/Tests/Messaging/`

If you add a new group, create a matching folder.

File name matches the outer class: `{Feature}Test.cs`.

The Tests.csproj uses SDK-style auto-include, so new .cs files are picked up automatically (no manual .csproj edit needed for this project).

## What makes a good test

- **Tests the actual behavior**, not implementation details. Call grain methods the way real code calls them.
- **Covers the happy path first**, then edge cases (empty collections, duplicate operations, invalid input).
- **Verifies persistence** — for state tests, write → read back → verify the data survived the round trip.
- **Uses realistic data** — real `CardType` values, plausible board sizes, meaningful user operations.
- **Cleans up after itself** — every created grain/user/match state is tracked for deletion.
- **Reports progress** — `SetProgress` at meaningful checkpoints, `Log` at key milestones.
