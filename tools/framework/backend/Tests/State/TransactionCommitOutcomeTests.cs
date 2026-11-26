using Common.Extensions;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State;

// A commit whose call fails after the server committed (the connection dropped while the answer was on the way):
// the transaction is done and must not be reported as failed, a caller would apply it again.
[Collection(nameof(OrleansIntegrationCollection))]
public class TransactionCommitOutcomeTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task CommitThrowsAfterServerCommitted_ReportedAsSuccess()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var transactions = CreateTransactions(commitThenThrow: true);

        var result = await transactions.Run(() => grain.Increment());

        result.IsSuccess.Should().BeTrue("the change is in the database: a failure result makes the caller apply it again");

        await grain.DeactivateAndWaitNewActivation();
        (await grain.Get()).Should().Be(1);
    }

    [Fact]
    public async Task CommitFailsBeforeServerCommitted_ReportedAsFailure()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var transactions = CreateTransactions(commitThenThrow: false);

        var result = await transactions.Run(() => grain.Increment());

        result.IsSuccess.Should().BeFalse();
        (await grain.Get()).Should().Be(0);
    }

    // The commit call fails and the server has not finished: the status is asked until the idle timeout of the
    // transaction (half of the stuck grace) aborts it.
    [Fact]
    public async Task CommitFailsAndServerAbortsLater_ReportedAsFailure()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var transactions = CreateNeverCommitting(GetSiloService<IDbSource>(), stuckGraceSeconds: 1f);

        var result = await transactions.Run(() => grain.Increment());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<NpgsqlException>();
        (await grain.Get()).Should().Be(0);
    }

    [Fact]
    public async Task CommitFailsAndServerNeverFinishes_OutcomeUnknown_ParticipantsRolledBack()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var transactions = CreateNeverCommitting(GetSiloService<IDbSource>(), stuckGraceSeconds: 600f);

        var run = () => transactions.Run(() => grain.Increment());

        var thrown = await run.Should().ThrowAsync<TransactionOutcomeUnknownException>();
        thrown.Which.InnerException.Should().BeOfType<NpgsqlException>();
        await AssertGrainIsFree(grain);
    }

    [Fact]
    public async Task CommitFailsAndStatusUnavailable_OutcomeUnknown_ParticipantsRolledBack()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());
        await using var unreachable = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=test;Timeout=1");

        // The first connection writes, the second one asks for the status of the transaction.
        var reachable = GetSiloService<IDbSource>().Value;
        var dbSource = Substitute.For<IDbSource>();
        dbSource.Value.Returns(reachable, unreachable);

        var transactions = CreateNeverCommitting(dbSource, stuckGraceSeconds: 600f);

        var run = () => transactions.Run(() => grain.Increment());

        var thrown = await run.Should().ThrowAsync<TransactionOutcomeUnknownException>();
        var inner = thrown.Which.InnerException.Should().BeOfType<AggregateException>().Subject;
        inner.InnerExceptions.Should().HaveCount(2);
        inner.InnerExceptions[0].Should().BeOfType<NpgsqlException>();
        await AssertGrainIsFree(grain);
    }

    private async Task AssertGrainIsFree(ITxTestGrain grain)
    {
        var next = await GetSiloService<ITransactions>().Run(() => grain.Increment());
        next.IsSuccess.Should().BeTrue();
    }

    private NeverCommittingTransactions CreateNeverCommitting(IDbSource dbSource, float stuckGraceSeconds)
    {
        var config = Substitute.For<ITransactionConfig>();
        config.Value.Returns(new TransactionOptions { StuckGraceSeconds = stuckGraceSeconds });

        return new NeverCommittingTransactions(
            GetSiloService<IStateStorage>(),
            GetSiloService<IEventStorage>(),
            dbSource,
            GetSiloService<ISideEffectsStorage>(),
            config);
    }

    private FaultyCommitTransactions CreateTransactions(bool commitThenThrow)
    {
        return new FaultyCommitTransactions(
            commitThenThrow,
            GetSiloService<IStateStorage>(),
            GetSiloService<IEventStorage>(),
            GetSiloService<IDbSource>(),
            GetSiloService<ISideEffectsStorage>(),
            GetSiloService<ITransactionConfig>());
    }

    private class FaultyCommitTransactions(
        bool commitThenThrow,
        IStateStorage stateStorage,
        IEventStorage eventStorage,
        IDbSource dbSource,
        ISideEffectsStorage sideEffectsStorage,
        ITransactionConfig transactionConfig)
        : Transactions(stateStorage, eventStorage, dbSource, sideEffectsStorage, new Lazy<ITransactionConfig>(() => transactionConfig),
            NullLogger<Transactions>.Instance)
    {
        protected override async Task CommitTransaction(NpgsqlTransaction transaction)
        {
            if (commitThenThrow == true)
                await transaction.CommitAsync();
            else
                await transaction.RollbackAsync();

            throw new NpgsqlException("Exception while reading from stream");
        }
    }

    // The commit call fails and the transaction stays open on the server.
    private class NeverCommittingTransactions(
        IStateStorage stateStorage,
        IEventStorage eventStorage,
        IDbSource dbSource,
        ISideEffectsStorage sideEffectsStorage,
        ITransactionConfig transactionConfig)
        : Transactions(stateStorage, eventStorage, dbSource, sideEffectsStorage, new Lazy<ITransactionConfig>(() => transactionConfig),
            NullLogger<Transactions>.Instance)
    {
        protected override Task CommitTransaction(NpgsqlTransaction transaction)
        {
            throw new NpgsqlException("Exception while reading from stream");
        }
    }
}
