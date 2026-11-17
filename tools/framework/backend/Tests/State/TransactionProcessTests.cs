using Common.Extensions;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Framework.Tests.State;

// Transactions.Process over participants it only knows by their handler: the order of the steps and who is told what.
[Collection(nameof(OrleansIntegrationCollection))]
public class TransactionProcessTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    private readonly List<string> _steps = new();

    [Fact]
    public async Task Success_CallbackThenSealThenConfirm()
    {
        var participant = Participant("a");

        var result = await GetSiloService<ITransactions>()
            .CreateBuilder(() => {
                Join(participant);
                return Task.CompletedTask;
            })
            .WithCallback(_ => {
                _steps.Add("callback");
                return Task.CompletedTask;
            })
            .Run();

        result.IsSuccess.Should().BeTrue();
        _steps.Should().Equal("callback", "a seal", "a success");
    }

    [Fact]
    public async Task ActionThrows_NoCommit_EveryParticipantRolledBackDespiteAFailingOne()
    {
        var failing = Participant("failing");
        failing.OnFailure(Arg.Any<Guid>()).ThrowsAsync(new InvalidOperationException("rollback"));
        var other = Participant("other");
        var error = new InvalidOperationException("action");

        var result = await GetSiloService<ITransactions>().Run(() => {
            Join(failing);
            Join(other);
            throw error;
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeSameAs(error);
        await failing.Received(1).OnFailure(Arg.Any<Guid>());
        await other.Received(1).OnFailure(Arg.Any<Guid>());
    }

    [Fact]
    public async Task CollectFails_NoCommit_ParticipantsRolledBack()
    {
        var error = new InvalidOperationException("collect");
        var failing = Participant("failing");
        failing.CollectResult(Arg.Any<Guid>()).ThrowsAsync(error);
        var other = Participant("other");

        var result = await GetSiloService<ITransactions>().Run(() => {
            JoinWithoutSnapshot(failing);
            Join(other);
            return Task.CompletedTask;
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeSameAs(error);
        _steps.Should().BeEquivalentTo("failing failure", "other failure");
    }

    [Fact]
    public async Task NoSnapshot_ResultIsAskedFromTheParticipant()
    {
        var participant = Participant("a");
        participant.CollectResult(Arg.Any<Guid>()).Returns(new TransactionHandlerResult { States = [], Events = [] });

        var result = await GetSiloService<ITransactions>().Run(() => {
            JoinWithoutSnapshot(participant);
            return Task.CompletedTask;
        });

        result.IsSuccess.Should().BeTrue();
        await participant.Received(1).CollectResult(Arg.Any<Guid>());
    }

    [Fact]
    public async Task SealRejected_NothingCommitted_ParticipantsRolledBack()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var error = new InvalidOperationException("seal");
        var rejecting = Participant("rejecting");
        rejecting.Seal(Arg.Any<Guid>()).ThrowsAsync(error);
        var callbackRan = false;

        var result = await GetSiloService<ITransactions>()
            .CreateBuilder(async () => {
                await grain.Increment();
                Join(rejecting);
            })
            .WithCallback(_ => {
                callbackRan = true;
                return Task.CompletedTask;
            })
            .Run();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeSameAs(error);
        callbackRan.Should().BeTrue("the seal is the last step before the commit");
        await rejecting.Received(1).OnFailure(Arg.Any<Guid>());
        await rejecting.DidNotReceive().OnSuccess(Arg.Any<Guid>());
        (await grain.Get()).Should().Be(0);

        await grain.DeactivateAndWaitNewActivation();
        (await grain.Get()).Should().Be(0);
    }

    [Fact]
    public async Task CallbackThrows_NothingCommitted_ParticipantsRolledBack()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var error = new InvalidOperationException("callback");
        var participant = Participant("a");

        var result = await GetSiloService<ITransactions>()
            .CreateBuilder(async () => {
                await grain.Increment();
                Join(participant);
            })
            .WithCallback(_ => throw error)
            .Run();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeSameAs(error);
        await participant.DidNotReceive().Seal(Arg.Any<Guid>());
        await participant.Received(1).OnFailure(Arg.Any<Guid>());
        (await grain.Get()).Should().Be(0);
    }

    [Fact]
    public async Task ConfirmFails_StillSuccess_NoRollback_EveryoneConfirmed()
    {
        var failing = Participant("failing");
        failing.OnSuccess(Arg.Any<Guid>()).ThrowsAsync(new InvalidOperationException("confirm"));
        var other = Participant("other");

        var result = await GetSiloService<ITransactions>().Run(() => {
            Join(failing);
            Join(other);
            return Task.CompletedTask;
        });

        result.IsSuccess.Should().BeTrue();
        await other.Received(1).OnSuccess(Arg.Any<Guid>());
        await failing.DidNotReceive().OnFailure(Arg.Any<Guid>());
        await other.DidNotReceive().OnFailure(Arg.Any<Guid>());
    }

    // The bound of the commit after the seal: half of the grace of the participants.
    [Fact]
    public async Task IdleInTransactionTimeout_IsHalfOfTheStuckGrace()
    {
        var config = Substitute.For<ITransactionConfig>();
        config.Value.Returns(new TransactionOptions { StuckGraceSeconds = 30f });

        var transactions = new Transactions(
            GetSiloService<IStateStorage>(),
            GetSiloService<IEventStorage>(),
            GetSiloService<IDbSource>(),
            GetSiloService<ISideEffectsStorage>(),
            new Lazy<ITransactionConfig>(() => config),
            NullLogger<Transactions>.Instance);

        object? timeout = null;

        var result = await transactions
            .CreateBuilder(() => Task.CompletedTask)
            .WithCallback(async transaction => {
                await using var command = transaction.Connection!.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "SHOW idle_in_transaction_session_timeout";
                timeout = await command.ExecuteScalarAsync();
            })
            .Run();

        result.IsSuccess.Should().BeTrue();
        timeout.Should().Be("15s");
    }

    private IGrainTransactionHandler Participant(string name)
    {
        // GetGrainId of Transactions.Process accepts a grain.
        var participant = Substitute.For<IGrainTransactionHandler, IGrainBase>();

        var context = Substitute.For<IGrainContext>();
        context.GrainId.Returns(GrainId.Create(GrainType.Create("test"), GrainIdKeyExtensions.CreateGuidKey(Guid.NewGuid())));
        ((IGrainBase)participant).GrainContext.Returns(context);

        participant.Seal(Arg.Any<Guid>()).Returns(_ => Step($"{name} seal"));
        participant.OnSuccess(Arg.Any<Guid>()).Returns(_ => Step($"{name} success"));
        participant.OnFailure(Arg.Any<Guid>()).Returns(_ => Step($"{name} failure"));

        return participant;
    }

    private Task Step(string step)
    {
        lock (_steps)
            _steps.Add(step);

        return Task.CompletedTask;
    }

    private static void Join(IGrainTransactionHandler participant)
    {
        var id = JoinWithoutSnapshot(participant);
        TransactionContextProvider.Current!.AddResult(id, new TransactionHandlerResult { States = [], Events = [] });
    }

    private static Guid JoinWithoutSnapshot(IGrainTransactionHandler participant)
    {
        var id = Guid.NewGuid();
        TransactionContextProvider.Current!.Participants.TryAdd(id, participant);
        return id;
    }
}
