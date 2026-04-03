using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests side effect registration, execution, and pipeline drain.
/// </summary>
[Collection(nameof(SideEffectIntegrationCollection))]
public class SideEffectTests(SideEffectTestFixture fixture) : IntegrationTestBase<SideEffectTestFixture>(fixture) {
    [Fact]
    public async Task SideEffect_RegisterAndDrain_ExecutesTargetGrain() {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        // Register a side effect that increments the target grain
        var sourceGrain = GetGrain<ISideEffectTestGrain>(sourceId);
        await sourceGrain.RegisterSideEffect(targetId);

        // Drain side effects
        var result = await DrainSideEffectsAsync();
        result.AssertDrainedWithWork();

        // Verify target grain was incremented
        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task SideEffect_MultipleDrain_AllExecuted() {
        var targetId = Guid.NewGuid();

        // Register 3 side effects targeting the same grain
        for (var i = 0; i < 3; i++) {
            var sourceGrain = GetGrain<ISideEffectTestGrain>(Guid.NewGuid());
            await sourceGrain.RegisterSideEffect(targetId);
        }

        // Drain all
        var result = await DrainSideEffectsAsync();
        result.AssertDrainedSuccessfully();

        // Verify target was incremented 3 times
        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(3);
    }

    [Fact]
    public async Task SideEffect_Transactional_ExecutesInsideTransaction() {
        var targetId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();

        await storage.Write(new TransactionalTestSideEffect { TargetGrainId = targetId });

        var result = await DrainSideEffectsAsync();
        result.AssertDrainedWithWork();

        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task SideEffect_Transactional_MultipleDrain_AllExecuted() {
        var targetId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();

        for (var i = 0; i < 3; i++)
            await storage.Write(new TransactionalTestSideEffect { TargetGrainId = targetId });

        var result = await DrainSideEffectsAsync();
        result.AssertDrainedSuccessfully();

        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(3);
    }

    [Fact]
    public async Task SideEffect_FailAndRetry_EventuallySucceeds() {
        FailingTestSideEffect.ResetAttempts();
        var targetId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();

        // SE that fails 2 times then succeeds on 3rd attempt
        await storage.Write(new FailingTestSideEffect { TargetGrainId = targetId, FailCount = 2 });

        // First pump — fails, goes to retry queue
        var pump1 = await Pipeline!.PumpOnceAsync();
        pump1.AllSucceeded.Should().BeFalse();

        // Wait for retry delay to expire, then requeue and pump again
        // IncrementalRetryDelay from config * (retryCount+1) — we need to wait or manipulate time.
        // Since test config has IncrementalRetryDelay=30s, we can't wait that long.
        // Instead, directly call RequeueStuck to simulate time passage — move processing→queue
        // Actually FailProcessing already moved it to retry_queue. We need RequeueReady but retry_after is in the future.
        // Workaround: pump multiple times with RequeueStuck between to force reprocessing.

        // The entry is now in retry_queue with retry_after in the future.
        // We'll use direct DB access to force-expire the retry_after.
        await ForceExpireRetryQueue();

        // Second pump — fails again (attempt 2)
        var pump2 = await Pipeline.PumpOnceAsync();
        pump2.AllSucceeded.Should().BeFalse();

        await ForceExpireRetryQueue();

        // Third pump — succeeds (attempt 3)
        var pump3 = await Pipeline.PumpOnceAsync();
        pump3.AllSucceeded.Should().BeTrue();

        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(1);

        FailingTestSideEffect.GetAttemptCount(targetId).Should().Be(3);
    }

    [Fact]
    public async Task SideEffect_MaxRetriesExceeded_Dropped() {
        var trackingId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();
        var config = GetSiloService<ISideEffectsConfig>();
        var maxRetries = config.Value.MaxRetryCount;

        await storage.Write(new AlwaysFailingSideEffect { TrackingId = trackingId });

        // Pump + force-expire retry for each attempt
        for (var i = 0; i < maxRetries + 1; i++) {
            await Pipeline!.PumpOnceAsync();
            await ForceExpireRetryQueue();
        }

        // After max retries, the entry should be deleted — no more work
        var finalPump = await Pipeline!.PumpOnceAsync();
        finalPump.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task SideEffect_RequeueStuck_RecoversCrashedEntries() {
        var targetId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();

        await storage.Write(new TestSideEffect { TargetGrainId = targetId });

        // Read moves entry from queue → processing (simulates worker picking it up)
        var entries = await storage.Read(1);
        entries.Should().HaveCount(1);

        // Queue should be empty now
        var emptyRead = await storage.Read(1);
        emptyRead.Should().BeEmpty();

        // Simulate crash recovery — move processing → queue
        await storage.RequeueStuck();

        // Now the entry should be back in the queue
        var result = await DrainSideEffectsAsync();
        result.AssertDrainedWithWork();

        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task SideEffect_EmptyQueue_DrainReturnsQuiet() {
        // No side effects registered — drain should return quietly
        var result = await Pipeline!.DrainUntilQuietAsync();
        result.ReachedQuiescence.Should().BeTrue();
        result.TotalTasks.Should().Be(0);
    }

    /// <summary>
    /// Force-expire all entries in retry queue by setting retry_after to past.
    /// Then call RequeueReady to move them back to main queue.
    /// </summary>
    private async Task ForceExpireRetryQueue() {
        await using var connection = await Database.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE side_effects_retry_queue SET retry_after = now() - interval '1 second'";
        await command.ExecuteNonQueryAsync();
        await GetSiloService<ISideEffectsStorage>().RequeueReady();
    }
}
