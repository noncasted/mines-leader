# Infrastructure Tests

Integration tests requiring Orleans TestCluster + PostgreSQL.

## Done

### State Read/Write — `State/StateReadWriteTests.cs` (4 tests)
- [x] Write and read single value
- [x] Write string label
- [x] Multiple writes keep latest
- [x] Different grains have independent state

### Transactions — `State/TransactionTests.cs` (4 tests)
- [x] Single increment commits
- [x] Multiple increments all applied
- [x] Rollback on exception — value unchanged
- [x] State persists after grain deactivation (reload from DB)

### Side Effects — `State/SideEffectTests.cs` (2 tests)
- [x] Register SE → drain → target grain updated
- [x] Multiple SEs → drain → all executed

### DurableQueue — `Messaging/DurableQueueTests.cs` (5 tests)
- [x] PushDirect single message delivered
- [x] PushDirect multiple messages all delivered
- [x] Multiple listeners all receive
- [x] Terminated listener — no delivery
- [x] PushTransactional — delivered after commit

### RuntimePipe — `Messaging/RuntimePipeTests.cs` (5 tests)
- [x] Send with handler returns response
- [x] Multiple requests each get correct response
- [x] No handler throws exception
- [x] Handler throws — error propagates
- [x] Async handler awaits correctly

### RuntimeChannel — `Messaging/RuntimeChannelTests.cs` (6 tests)
- [x] Single subscriber receives
- [x] Multiple subscribers all receive
- [x] Multiple messages delivered in order
- [x] Different channels isolated
- [x] Terminated listener — no delivery
- [x] No subscribers — does not throw

## Todo

### State Migrations
- [ ] Write V0 state → read as V1 via migration step
- [ ] Concurrent reads during migration

### StateCollection Sync
- [ ] OnUpdated pushes to collection
- [ ] OnUpdatedTransactional within transaction
- [ ] Collection reflects grain state changes

### Transactions — Advanced
- [ ] Chained transaction (multiple grains in one TX)
- [ ] Chained fail — all rollback
- [ ] Concurrent transactions on same grain
- [ ] Overlapping transactions — conflict resolution
- [ ] Mid-chain fail — partial rollback
- [ ] Large batch transaction (10+ grains)
- [ ] Empty transaction — no state changes
- [ ] Transaction takeover — stuck TX cleanup

### Side Effects — Advanced
- [ ] Transactional side effect (ITransactionalSideEffect)
- [ ] Retry on failure with incremental delay
- [ ] Max retries exceeded — dropped
