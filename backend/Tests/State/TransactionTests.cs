using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests custom transaction system: commit, rollback, concurrent access.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class TransactionTests(OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture) {
    [Fact]
    public async Task Transaction_Increment_Success() {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        await RunTransaction(() => grain.Increment());

        var value = await grain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task Transaction_MultipleIncrements_AllApplied() {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        for (var i = 0; i < 5; i++)
            await RunTransaction(() => grain.Increment());

        var value = await grain.Get();
        value.Should().Be(5);
    }

    [Fact]
    public async Task Transaction_Rollback_ValueUnchanged() {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        // First set a known value
        await RunTransaction(() => grain.Increment());

        // Attempt transaction that fails
        var transactions = GetSiloService<ITransactions>();
        var result = await transactions.Run(async () => {
            await grain.Increment();
            throw new Exception("Intentional rollback");
        });

        result.IsSuccess.Should().BeFalse();

        // Value should still be 1 (not 2)
        var value = await grain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task Transaction_Empty_Succeeds() {
        var transactions = GetSiloService<ITransactions>();
        var result = await transactions.Run(() => Task.CompletedTask);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Transaction_MidChainFail_BothRolledBack() {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var grainA = GetGrain<ITxTestGrain>(idA);
        var grainB = GetGrain<ITxTestGrain>(idB);

        var transactions = GetSiloService<ITransactions>();
        var result = await transactions.Run(async () => {
            await grainA.Increment();
            await grainB.Increment();
            throw new Exception("Failure after both grains incremented");
        });

        result.IsSuccess.Should().BeFalse();

        var valueA = await grainA.Get();
        var valueB = await grainB.Get();
        valueA.Should().Be(0);
        valueB.Should().Be(0);

        // Verify grains are usable after rollback
        await RunTransaction(async () => {
            await grainA.Increment();
            await grainB.Increment();
        });

        var finalA = await grainA.Get();
        var finalB = await grainB.Get();
        finalA.Should().Be(1);
        finalB.Should().Be(1);
    }

    [Fact]
    public async Task Transaction_Takeover_SecondTransactionSucceeds() {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        var transactions = GetSiloService<ITransactions>();

        // Start slow transaction that holds grain lock for 15s (> 5s StuckGraceSeconds test threshold)
        var slowTask = transactions.Run(() => grain.IncrementWithDelay(15_000));

        // Wait for slow transaction to acquire the lock
        await Task.Delay(2000);

        // Second transaction should wait, then takeover after StuckGraceSeconds
        var fastResult = await transactions.Run(() => grain.Increment());
        fastResult.IsSuccess.Should().BeTrue();

        // Wait for slow transaction to complete (should have been taken over)
        var slowResult = await slowTask;
        slowResult.IsSuccess.Should().BeFalse();

        var value = await grain.Get();
        value.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Transaction_StatePersistsAfterDeactivation() {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        await RunTransaction(() => grain.Increment());
        await RunTransaction(() => grain.Increment());
        await RunTransaction(() => grain.Increment());

        // Force grain deactivation — next call will reload from DB
        await grain.Deactivate();
        await Task.Delay(2000);

        var value = await grain.Get();
        value.Should().Be(3);
    }
}
