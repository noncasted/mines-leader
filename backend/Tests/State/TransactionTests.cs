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
