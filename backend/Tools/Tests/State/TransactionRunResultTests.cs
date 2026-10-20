using FluentAssertions;
using Infrastructure;
using NSubstitute;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// F2.37: the value-returning Run&lt;T&gt; (and InTransaction) throw TransactionFailedException with the original
/// error when the transaction does not commit, instead of returning the value of an uncommitted action.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class TransactionRunResultTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task RunT_ActionThrows_ThrowsWithOriginalError()
    {
        var grain = GetGrain<ITxSideEffectGrain>(Guid.NewGuid());
        var transactions = GetSiloService<ITransactions>();
        var error = new InvalidOperationException("action failed");

        var act = () => transactions.Run<int>(async () => {
            await grain.IncrementAndReturn();
            throw error;
        });

        (await act.Should().ThrowAsync<TransactionFailedException>()).Which.InnerException.Should().BeSameAs(error);
        (await grain.Get()).Should().Be(0, "the failed transaction is rolled back");
    }

    [Fact]
    public async Task RunT_ReferenceTypeNullResult_IsReturned()
    {
        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(() => Task.FromResult<string?>(null));

        result.Should().BeNull("null is a valid result of a committed transaction");
    }

    [Fact]
    public async Task RunT_CommitFailsAfterAction_Throws()
    {
        // The action completes and produces a value, then the commit fails.
        var commitError = new InvalidOperationException("commit failed");
        var transactions = Substitute.For<ITransactions>();

        transactions.Process(Arg.Any<TransactionParameters>()).Returns(async call => {
            await call.Arg<TransactionParameters>().Action();
            return new TransactionResult { IsSuccess = false, Error = commitError };
        });

        var act = () => transactions.Run(() => Task.FromResult(42));

        (await act.Should().ThrowAsync<TransactionFailedException>()).Which.InnerException.Should().BeSameAs(commitError);
    }

    [Fact]
    public async Task InTransaction_Fails_Throws()
    {
        var orleans = GetSiloService<IOrleans>();
        var error = new InvalidOperationException("action failed");

        var act = () => orleans.InTransaction(() => throw error);

        (await act.Should().ThrowAsync<TransactionFailedException>()).Which.InnerException.Should().BeSameAs(error);
    }
}
