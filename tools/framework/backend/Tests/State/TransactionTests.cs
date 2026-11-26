using FluentAssertions;
using Infrastructure;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Xunit;

namespace Framework.Tests.State;

[Collection(nameof(OrleansIntegrationCollection))]
public class TransactionTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Transaction_Increment_Success()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        await RunTransaction(() => grain.Increment());

        var value = await grain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task Transaction_MultipleIncrements_AllApplied()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        for (var i = 0; i < 5; i++)
            await RunTransaction(() => grain.Increment());

        var value = await grain.Get();
        value.Should().Be(5);
    }

    [Fact]
    public async Task Transaction_Rollback_ValueUnchanged()
    {
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
    public async Task Transaction_Empty_Succeeds()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        // Set a known value before empty transaction
        await RunTransaction(() => grain.Increment());
        var valueBefore = await grain.Get();
        valueBefore.Should().Be(1);

        // Run empty transaction — should succeed without modifying state
        var transactions = GetSiloService<ITransactions>();
        var result = await transactions.Run(() => Task.CompletedTask);
        result.IsSuccess.Should().BeTrue();

        // Verify state was not modified by the empty transaction
        var valueAfter = await grain.Get();
        valueAfter.Should().Be(1);
    }

    [Fact]
    public async Task Transaction_MidChainFail_BothRolledBack()
    {
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
    public async Task Transaction_Takeover_SecondTransactionSucceeds()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        var transactions = GetSiloService<ITransactions>();
        var hold = TestGate.Create();

        // The lock wait is longer than the grace: when the second Join times out the first transaction is past its
        // grace however late the second one came, so the takeover does not depend on the speed of the machine.
        using var timings = OverrideConfig<ITransactionConfig, TransactionOptions>(new TransactionOptions
        {
            LockWaitSeconds = 0.4f,
            StuckGraceSeconds = 0.2f
        });

        // The slow transaction holds the grain inside a grain call until the takeover is done.
        var slowTask = transactions.Run(() => grain.IncrementAndHold(hold.Id));
        await hold.Entered;

        // The second transaction waits for the lock, then takes the grain over. The grace also bounds its commit
        // (TransactionWriteBatch): the values of the fixture are back before that.
        var fastResult = await transactions.Run(async () => {
            await grain.Increment();
            timings.Dispose();
        });

        hold.Release();
        fastResult.IsSuccess.Should().BeTrue();

        // The slow transaction was taken over: it must not commit.
        var slowResult = await slowTask;
        slowResult.IsSuccess.Should().BeFalse();

        var value = await grain.Get();
        value.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Transaction_StatePersistsAfterDeactivation()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        await RunTransaction(() => grain.Increment());
        await RunTransaction(() => grain.Increment());
        await RunTransaction(() => grain.Increment());

        // The next call reloads from the database.
        await grain.DeactivateAndWaitNewActivation();

        var value = await grain.Get();
        value.Should().Be(3);
    }

    [Fact]
    public async Task Transaction_ChainedThreeGrains_AllCommitted()
    {
        var grainA = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var grainB = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var grainC = GetGrain<ITxTestGrain>(Guid.NewGuid());

        await RunTransaction(async () => {
            await grainA.Increment();
            await grainB.Increment();
            await grainC.Increment();
        });

        var valueA = await grainA.Get();
        var valueB = await grainB.Get();
        var valueC = await grainC.Get();

        valueA.Should().Be(1);
        valueB.Should().Be(1);
        valueC.Should().Be(1);
    }

    [Fact]
    public async Task Transaction_ChainedThreeGrainsFail_AllRolledBack()
    {
        var grainA = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var grainB = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var grainC = GetGrain<ITxTestGrain>(Guid.NewGuid());

        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(async () => {
            await grainA.Increment();
            await grainB.Increment();
            await grainC.Increment();
            throw new Exception("Fail after all three grains incremented");
        });

        result.IsSuccess.Should().BeFalse();

        var valueA = await grainA.Get();
        var valueB = await grainB.Get();
        var valueC = await grainC.Get();

        valueA.Should().Be(0);
        valueB.Should().Be(0);
        valueC.Should().Be(0);
    }

    [Fact]
    public async Task Transaction_ConcurrentOnSameGrain_OneFailsOrBothSucceedSequentially()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        var transactions = GetSiloService<ITransactions>();

        // Launch two transactions on the same grain concurrently
        var task1 = transactions.Run(() => grain.Increment());
        var task2 = transactions.Run(() => grain.Increment());

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.IsSuccess);

        // At least one must succeed; both may succeed if serialized properly
        successCount.Should().BeGreaterThanOrEqualTo(1);

        var value = await grain.Get();
        value.Should().Be(successCount);
    }

    [Fact]
    public async Task Transaction_LargeBatch_TenGrainsAllCommitted()
    {
        var grains = Enumerable.Range(0, 10)
                               .Select(_ => GetGrain<ITxTestGrain>(Guid.NewGuid()))
                               .ToList();

        // Verify all grains start at zero
        foreach (var grain in grains)
        {
            var initial = await grain.Get();
            initial.Should().Be(0);
        }

        await RunTransaction(async () => {
            foreach (var grain in grains)
                await grain.Increment();
        });

        // Verify each grain individually was incremented exactly once
        var values = new List<int>();

        foreach (var grain in grains)
        {
            var value = await grain.Get();
            value.Should().Be(1);
            values.Add(value);
        }

        // Verify all 10 committed (not partial)
        values.Should().HaveCount(10);
        values.Should().AllSatisfy(v => v.Should().Be(1));
    }

    [Fact]
    public async Task Transaction_MidChainPartialFail_FirstTwoRolledBack()
    {
        var grainA = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var grainB = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var grainC = GetGrain<ITxTestGrain>(Guid.NewGuid());

        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(async () => {
            await grainA.Increment();
            await grainB.Increment();
            // Fail before touching grainC
            throw new Exception("Fail after two out of three grains");
        });

        result.IsSuccess.Should().BeFalse();

        // Both touched grains must be rolled back
        var valueA = await grainA.Get();
        var valueB = await grainB.Get();
        var valueC = await grainC.Get();

        valueA.Should().Be(0);
        valueB.Should().Be(0);
        valueC.Should().Be(0);

        // Verify all three grains remain usable
        await RunTransaction(async () => {
            await grainA.Increment();
            await grainB.Increment();
            await grainC.Increment();
        });

        (await grainA.Get()).Should().Be(1);
        (await grainB.Get()).Should().Be(1);
        (await grainC.Get()).Should().Be(1);
    }

    [Fact]
    public async Task Transaction_ReturnValue_ReturnsResult()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxSideEffectGrain>(id);

        var transactions = GetSiloService<ITransactions>();
        var result = await transactions.Run(async () => await grain.IncrementAndReturn());

        result.Should().Be(1);

        var value = await grain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task Transaction_ReturnValue_MultipleIncrements()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxSideEffectGrain>(id);

        var transactions = GetSiloService<ITransactions>();

        var r1 = await transactions.Run(async () => await grain.IncrementAndReturn());
        var r2 = await transactions.Run(async () => await grain.IncrementAndReturn());
        var r3 = await transactions.Run(async () => await grain.IncrementAndReturn());

        r1.Should().Be(1);
        r2.Should().Be(2);
        r3.Should().Be(3);
    }

    [Fact]
    public async Task TransactionBuilder_WithCallback_CallbackExecuted()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);
        var callbackExecuted = false;

        var transactions = GetSiloService<ITransactions>();

        var result = await transactions
                           .CreateBuilder(() => grain.Increment())
                           .WithCallback(tx => {
                               callbackExecuted = true;
                               return Task.CompletedTask;
                           })
                           .Run();

        result.IsSuccess.Should().BeTrue();
        callbackExecuted.Should().BeTrue();

        var value = await grain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task TransactionBuilder_MultipleCallbacks_AllExecuted()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);
        var callbackCount = 0;

        var transactions = GetSiloService<ITransactions>();

        var result = await transactions
                           .CreateBuilder(() => grain.Increment())
                           .WithCallback(tx => {
                               callbackCount++;
                               return Task.CompletedTask;
                           })
                           .WithCallback(tx => {
                               callbackCount++;
                               return Task.CompletedTask;
                           })
                           .WithCallback(tx => {
                               callbackCount++;
                               return Task.CompletedTask;
                           })
                           .Run();

        result.IsSuccess.Should().BeTrue();
        callbackCount.Should().Be(3);
    }

    [Fact]
    public async Task Transaction_SameGrainTwice_BothMutationsApplied()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        await RunTransaction(async () => {
            await grain.Increment();
            await grain.Increment();
        });

        var value = await grain.Get();
        value.Should().Be(2);
    }

    [Fact]
    public async Task Transaction_SameGrainTwice_LastSnapshotPersisted()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);
        var other = GetGrain<ITxTestGrain>(Guid.NewGuid());

        // Snapshot of `grain` is taken on every [Transaction] return; the second call must win in the DB.
        await RunTransaction(async () => {
            await grain.Increment();
            await other.Increment();
            await grain.Increment();
        });

        await grain.DeactivateAndWaitNewActivation();

        var value = await grain.Get();
        value.Should().Be(2);
    }

    [Fact]
    public async Task Transaction_ParallelCallsSameGrain_LastSnapshotPersisted()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        // Two parallel branches hit the same grain; the slower branch returns last with an older or newer
        // snapshot depending on scheduling. Either way the DB must end up with both increments.
        await RunTransaction(() => Task.WhenAll(grain.IncrementWithDelay(200), grain.Increment()));

        await grain.DeactivateAndWaitNewActivation();

        var value = await grain.Get();
        value.Should().Be(2);
    }

    // A reentrant grain, busy with another transaction, starts both parallel calls of a transaction: two Joins of one
    // transaction wait for its lock, and the second must not wait for the first.
    [Fact]
    public async Task Transaction_ParallelCallsOnABusyGrain_BothJoinAndCommit()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        var transactions = GetSiloService<ITransactions>();
        var hold = TestGate.Create();

        var holder = transactions.Run(() => grain.IncrementAndHold(hold.Id));
        await hold.Entered;

        var parallel = transactions.Run(async () => {
            var calls = Task.WhenAll(grain.Increment(), grain.Increment());

            // Answered behind both calls: the activation has started them, they wait in their Joins.
            await grain.GetActivationId();

            hold.Release();
            await calls;
        });

        (await holder).IsSuccess.Should().BeTrue();
        var result = await parallel;
        result.IsSuccess.Should().BeTrue(result.Error?.Message);

        await grain.DeactivateAndWaitNewActivation();

        (await grain.Get()).Should().Be(3);
    }

    [Fact]
    public async Task Transaction_RollbackError_ContainsException()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(async () => {
            await grain.Increment();
            throw new InvalidOperationException("test-error-message");
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("test-error-message");
    }
}