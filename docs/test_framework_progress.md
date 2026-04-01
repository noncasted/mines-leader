# xUnit Test Framework Implementation Progress

## Status: COMPLETE (v1)

All 10 example tests pass. Framework is ready for writing domain tests.

## What was done

### [x] 1. Rename Tests -> Benchmarks
- Renamed folder, .csproj, namespaces
- Updated all references in Console.csproj, Silo.csproj, Extensions.csproj
- Updated Razor pages, ProjectsSetupExtensions.cs

### [x] 2. Create new backend/Tests xUnit project
- Tests.csproj with xUnit 3, FluentAssertions, Testcontainers.PostgreSql, Orleans.TestingHost, NSubstitute
- Central Package Management versions in Directory.Packages.props

### [x] 3. DatabaseFixture
- Testcontainers PostgreSQL 17 (shared container, unique DB per fixture)
- Auto-creates state tables from StatesLookup.All with correct column types (uuid/varchar/bigint)
- Creates side effects tables (queue, processing, retry_queue)
- Per-test TRUNCATE via ResetDatabaseAsync()

### [x] 4. OrleansTestClusterFixture
- InProcessTestClusterBuilder with 1 silo
- Full DI: StateStorage, StateSerializer, Transactions, Messaging, SideEffectsStorage
- All IAddressableState configs mocked via TestAddressableState + NSubstitute
- GrainTransactionHandler extension registered
- Virtual ConfigureSiloServices/ConfigureSilo for domain fixtures
- xUnit Collection: OrleansIntegrationCollection

### [x] 5. SideEffectTestFixture + Pipeline
- SideEffectTestPipeline with PumpOnceAsync, SettleAndDrainAsync, DrainUntilQuietAsync
- PumpResult, DrainResult records
- DrainResultExtensions: AssertDrainedSuccessfully, AssertDrainedWithWork, AssertAllSucceeded
- xUnit Collection: SideEffectIntegrationCollection

### [x] 6. IntegrationTestBase
- Generic IntegrationTestBase<TFixture> with IAsyncLifetime
- Per-test cleanup in InitializeAsync (ResetDatabaseAsync)
- Helpers: RunTransaction, GetGrain, GetSiloService, DrainSideEffectsAsync

### [x] 7. Test grains + Example tests
- SimpleTestGrain (state read/write)
- TxTestGrain (transactional increment with persistence)
- SideEffectTestGrain + TestSideEffect (SE registration and execution)
- 4 state tests, 4 transaction tests, 2 side effect tests — all green

## Test Results
```
Total tests: 10
     Passed: 10
Total time: ~6.5 seconds
```

## Files Created

```
backend/Tests/
  Tests.csproj
  Fixtures/
    DatabaseFixture.cs           - PostgreSQL test container
    OrleansTestClusterFixture.cs - Orleans TestCluster setup
    SideEffectTestFixture.cs     - SE pipeline fixture
    SideEffectTestPipeline.cs    - Pump/drain engine
    IntegrationTestBase.cs       - Per-test lifecycle base
    TestAddressableState.cs      - Mock for IAddressableState configs
  Grains/
    TestGrains.cs                - Test grain implementations
  State/
    StateReadWriteTests.cs       - Basic state R/W tests
    TransactionTests.cs          - Transaction commit/rollback/persistence
    SideEffectTests.cs           - SE registration and drain
```

## Files Modified

```
backend/Benchmarks/              - Renamed from Tests
  Benchmarks.csproj              - Was Tests.csproj
  *.cs                           - namespace Tests -> Benchmarks

backend/Console/Console.csproj            - Tests.csproj -> Benchmarks.csproj ref
backend/Orchestration/Silo/Silo.csproj    - Tests.csproj -> Benchmarks.csproj ref
backend/Orchestration/Extensions/Extensions.csproj         - Same
backend/Orchestration/Extensions/ProjectsSetupExtensions.cs - using Tests -> Benchmarks
backend/Console/Pages/Tests/TestEntry.razor  - @using global::Benchmarks
backend/Console/Pages/Tests/Tests.razor      - @using global::Benchmarks
backend/Common/Lookups/StatesLookup.cs       - Added SimpleTest, TxTest entries
backend/Directory.Packages.props             - Added test package versions
```

## Architecture

```
OrleansTestClusterFixture (IAsyncLifetime)
  ├── DatabaseFixture (Testcontainers PostgreSQL)
  ├── InProcessTestCluster (1 silo)
  └── Full DI: State, Transactions, Messaging, SideEffects
       │
       ├── SideEffectTestFixture (extends)
       │   └── SideEffectTestPipeline (pump/drain)
       │
       └── [Future domain fixtures]

IntegrationTestBase<TFixture> (IAsyncLifetime)
  ├── Per-test DB cleanup
  ├── Transaction helpers
  └── Grain access helpers
```

## How to write new tests

1. Create test class with `[Collection(nameof(OrleansIntegrationCollection))]`
2. Inherit from `IntegrationTestBase<OrleansTestClusterFixture>`
3. Use `GetGrain<T>()`, `RunTransaction()`, `GetSiloService<T>()`
4. For side effects: use `SideEffectTestFixture` collection + `DrainSideEffectsAsync()`
5. New grain types: add state to StatesLookup + register in OrleansTestClusterFixture.BuildStatesRegistry()
