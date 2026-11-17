using Common.Extensions;
using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Xunit;

namespace Framework.Tests.State;

// a participant failing to confirm (OnSuccess) after the database commit must not turn the transaction
// into a failure: the data is committed, so the result is success and no participant is rolled back.
[Collection(nameof(OrleansIntegrationCollection))]
public class TransactionConfirmFailureTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task OnSuccessThrowsAfterCommit_ResultIsSuccess_NoRollback()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxConfirmFailingGrain>(id);
        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(() => grain.Write(42));

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();

        (await grain.GetConfirmCalls()).Should().Be(1);
        (await grain.GetRollbackCalls()).Should().Be(0);
        (await ReadCommitted(id)).Should().Be(42);
    }

    [Fact]
    public async Task OnSuccessThrowsAfterCommit_GrainIsReleasedForNextTransaction()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxConfirmFailingGrain>(id);
        var transactions = GetSiloService<ITransactions>();

        (await transactions.Run(() => grain.Write(1))).IsSuccess.Should().BeTrue();

        // Without the release in GrainTransactionHandler.OnSuccess the second Join waits LockWaitSeconds and throws.
        var second = transactions.Run(() => grain.Write(2));
        var completed = await Task.WhenAny(second, Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));

        completed.Should().BeSameAs(second);
        (await second).IsSuccess.Should().BeTrue();

        (await grain.GetConfirmCalls()).Should().Be(2);
        (await grain.GetRollbackCalls()).Should().Be(0);
        (await ReadCommitted(id)).Should().Be(2);
    }

    [Fact]
    public async Task ActionThrows_StillRollsBack()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxConfirmFailingGrain>(id);
        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(async () => {
            await grain.Write(7);
            throw new InvalidOperationException("Intentional failure before commit");
        });

        result.IsSuccess.Should().BeFalse();
        (await grain.GetConfirmCalls()).Should().Be(0);
        (await grain.GetRollbackCalls()).Should().Be(1);
    }

    private async Task<int> ReadCommitted(Guid id)
    {
        var storage = GetSiloService<IStateStorage>();
        var stateInfo = storage.Registry.Get<TxTestState>();

        var state = await storage.Read<TxTestState>(new StateIdentity
        {
            Key = id,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        });

        return state.Value;
    }
}
