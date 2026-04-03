# Meta Benchmarks

## Overview

Tests meta-game systems: user creation and progression (XP/rating), deck management (read/update), and full match lifecycle (setup, completion, rating application, history recording). Uses `IUserFactory` for user creation and `ITransactions` for atomic operations.

## Metric

`ms` — auto-fallback to `DurationMs` (no manual `ReportMetric()` call).

---

## Benchmarks

### user-progression-rating
- **File**: `backend/Benchmarks/Meta/UserProgressionRatingTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: User progression and rating systems. Creates user, adds win/loss records, verifies XP is cumulative while rating can be negative.
- **Distributed**: No

### user-deck
- **File**: `backend/Benchmarks/Meta/UserDeckTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Deck management. Creates user with default decks, reads deck state, updates deck 0 with custom cards, verifies persistence.
- **Distributed**: No

### match-recording
- **File**: `backend/Benchmarks/Meta/MatchRecordingTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Full match lifecycle. Creates 2 users + match, sets type to TimeLimited, completes with winner, verifies progression/rating applied to both players and match history entries exist.
- **Distributed**: No

---

## TODO

### Throughput (ops/s)
- [ ] **user-auth-check** — `IUserAuth.IsExists()` transactional read throughput. High-frequency operation on every login.
- [ ] **user-set-name** — `IUser.SetName()` transactional write + projection push + collection sync. Measures cross-grain write chain.
- [ ] **user-projection-send** — `IUserProjection.SendCached()` throughput: cache write + conditional RuntimeChannel publish. Hot path for every state change visible to user.
- [ ] **match-factory-create** — `IMatchFactory.Create()` full flow: grain setup + ServiceDiscovery + pipe send to GameGateway. Measures match creation throughput.

### Correctness (ms)
- [ ] **user-match-history-depth** — `IUserMatchHistory.GetBlock()` read latency at history depths N=10/100/1000. Measures TakeLast cost growth.
- [ ] **user-match-history-append** — `IUserMatchHistory.Add()` transactional append + projection push. Measures write cost including side effects.
- [ ] **match-setup-parallel** — `IMatch.Setup()` isolated: parallel `GetSelected()` deck reads for 2 users + state write. Measures deck-fetch parallelism cost.
- [ ] **bot-factory-create** — `IBotFactory.Create()` full bot user initialization: IUser + IUserDeck + BotCollection update.
- [ ] **matchmaking-queue-throughput** — `IMatchmaking.SearchMatch()` under N=10/50/100 concurrent searchers. Measures SemaphoreSlim lock contention and pairing throughput per loop tick.
- [ ] **user-projection-reconnect** — `IUserProjection.OnConnected()` + `ForceNotify()` full reconnection cycle: all cached payloads replayed to channel.
