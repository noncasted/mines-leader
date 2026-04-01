# Test Coverage Tracker

## Summary

| Scope | Done | Todo | Total |
|-------|------|------|-------|
| [Infrastructure](infrastructure.md) | 26 | 16 | 42 |
| [Cards — Board](cards-board.md) | 19 | 26 | 45 |
| [Cards — Player](cards-player.md) | 0 | 22 | 22 |
| [Board Mechanics](board-mechanics.md) | 50 | 0 | 50 |
| [Player Mechanics](player-mechanics.md) | 0 | 32 | 32 |
| [Meta Services](meta-services.md) | 0 | 21 | 21 |
| **Total** | **95** | **117** | **212** |

Game tests are pure unit tests (no Orleans). Infrastructure and Meta are integration tests with TestCluster + PostgreSQL.

## Architecture

```
backend/Tests/
  Fixtures/           -- test infrastructure (fixtures, helpers)
  docs/               -- this tracker
  State/              -- state, transactions, side effects (integration)
  Messaging/          -- durable queue, pipe, channel (integration)
  Game/               -- board, cards, reveal (unit)
  Grains/             -- test grain implementations
```

## Test Types

- **Unit** (Game/) — pure logic, no IO, < 1ms per test. Cards, board, player stats.
- **Integration** (State/, Messaging/) — Orleans TestCluster + PostgreSQL in Docker. ~5s startup, ~10ms per test.
- **Meta** (planned) — grain-level business logic. Same infra as integration.

## How to run

```bash
dotnet test backend/Tests/Tests.csproj                                          # all (95 tests, ~5s)
dotnet test backend/Tests/Tests.csproj --filter "FullyQualifiedName~Tests.Game"  # game only (~0.7s)
dotnet test backend/Tests/Tests.csproj --filter "FullyQualifiedName~Tests.State" # state + tx
dotnet test backend/Tests/Tests.csproj --filter "FullyQualifiedName~Messaging"   # messaging
```

## Conventions

- Board tests use visual `BoardParser.Parse()` / `BoardParser.AssertBoard()` format
- Card configs from production JSON via `CardConfigs.*`
- `TestBoardBuilder` for programmatic board setup
- `IPlayer` mocked via NSubstitute for cards that need player
- `[Collection(nameof(OrleansIntegrationCollection))]` for integration tests sharing a cluster
