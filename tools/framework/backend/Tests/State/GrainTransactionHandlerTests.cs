using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using Framework.Tests.Grains;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Framework.Tests.State;

// The handler alone, without a cluster: who holds the grain, the wait and the takeover, and what a transaction changed.
public class GrainTransactionHandlerTests
{
    public GrainTransactionHandlerTests()
    {
        var config = Substitute.For<ITransactionConfig>();
        config.Value.Returns(_ => _options);
        _handler = new GrainTransactionHandler(config, NullLogger<GrainTransactionHandler>.Instance);
    }

    private static readonly TransactionOptions _noWait = new() { LockWaitSeconds = 0.01f, StuckGraceSeconds = 600f };
    private static readonly TransactionOptions _stuckAtOnce = new() { LockWaitSeconds = 0.01f, StuckGraceSeconds = 0f };
    private static readonly TransactionOptions _longWait = new() { LockWaitSeconds = 60f, StuckGraceSeconds = 600f };

    private readonly GrainTransactionHandler _handler;
    private readonly Guid _first = Guid.NewGuid();
    private readonly Guid _second = Guid.NewGuid();
    private readonly Guid _third = Guid.NewGuid();
    private TransactionOptions _options = _noWait;

    [Fact]
    public async Task Join_SameTransactionTwice_SameParticipantWithoutWaiting()
    {
        var participant = await _handler.Join(_first);

        (await _handler.Join(_first)).Should().Be(participant);
    }

    [Fact]
    public async Task Join_OtherTransactionWithinGrace_ThrowsAndTheFirstKeepsTheGrain()
    {
        var state = Substitute.For<IGrainStateTransactionParticipant>();
        await _handler.Join(_first);
        _handler.RecordStateChanged(state);

        var join = () => _handler.Join(_second);

        await join.Should().ThrowAsync<Exception>().WithMessage($"*'{_second}'*'{_first}'*");
        state.DidNotReceive().OnTransactionFailure();
        await _handler.Seal(_first);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Join_OtherTransaction_WaitsUntilTheFirstEnds(bool success)
    {
        var participant = await _handler.Join(_first);

        _options = _longWait;
        var waiting = _handler.Join(_second);
        waiting.IsCompleted.Should().BeFalse();

        await (success ? _handler.OnSuccess(_first) : _handler.OnFailure(_first));

        (await waiting).Should().Be(participant);
        await _handler.Seal(_second);
    }

    [Fact]
    public async Task Join_StuckPastGrace_RollsBackTheStuckChangesAndTakesTheGrain()
    {
        var state = Substitute.For<IGrainStateTransactionParticipant>();
        var events = Substitute.For<IGrainEventTransactionParticipant>();
        events.StreamId.Returns("stream");

        var participant = await _handler.Join(_first);
        _handler.RecordStateChanged(state);
        _handler.RecordEventStateChanged(events, [Payload()]);

        _options = _stuckAtOnce;
        (await _handler.Join(_second)).Should().Be(participant);

        state.Received(1).OnTransactionFailure();
        events.Received(1).OnTransactionFailure();
        state.DidNotReceive().OnTransactionSuccess();

        var result = await _handler.CollectResult(_second);
        result.States.Should().BeEmpty();
        result.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task TakenOverTransaction_IsRejectedAndTouchesNothing()
    {
        await _handler.Join(_first);

        _options = _stuckAtOnce;
        await _handler.Join(_second);

        var state = Substitute.For<IGrainStateTransactionParticipant>();
        _handler.RecordStateChanged(state);

        var seal = () => _handler.Seal(_first);
        var collect = () => _handler.CollectResult(_first);

        await seal.Should().ThrowAsync<Exception>().WithMessage($"*'{_first}'*'{_second}'*");
        await collect.Should().ThrowAsync<Exception>().WithMessage($"*'{_first}'*'{_second}'*");

        await _handler.OnSuccess(_first);
        await _handler.OnFailure(_first);

        state.DidNotReceive().OnTransactionSuccess();
        state.DidNotReceive().OnTransactionFailure();

        // The lock stays with the one that took over: the late end of the old transaction released nothing.
        _options = _noWait;
        var join = () => _handler.Join(_third);
        await join.Should().ThrowAsync<Exception>().WithMessage($"*'{_third}'*'{_second}'*");
        await _handler.Seal(_second);
    }

    // The lock passes from the stuck transaction to the one that took over: the one waiting longer keeps its wait, not a
    // failure at the moment of the takeover, and gets the grain after it.
    [Fact]
    public async Task Join_TakeoverWhileAnotherWaits_TheWaitingOneGetsTheGrainAfterTheTakeover()
    {
        await _handler.Join(_first);

        _options = _longWait;
        var waiting = _handler.Join(_second);

        _options = _stuckAtOnce;
        await _handler.Join(_third);

        await _handler.Seal(_third);
        waiting.IsCompleted.Should().BeFalse();
        await _handler.OnSuccess(_third);

        await waiting;
        await _handler.Seal(_second);
    }

    [Fact]
    public async Task CollectResult_SnapshotOfTheChanges_SequenceGrows()
    {
        var value = TxTestState.Of(5);
        var state = Substitute.For<IGrainStateTransactionParticipant>();
        state.GetState().Returns(value);

        var recorded = Substitute.For<IGrainEventTransactionParticipant>();
        recorded.StreamId.Returns("recorded");
        var withoutPayloads = Substitute.For<IGrainEventTransactionParticipant>();
        withoutPayloads.StreamId.Returns("without-payloads");
        var payload = Payload();

        await _handler.Join(_first);
        _handler.RecordStateChanged(state);
        _handler.RecordStateChanged(state);
        _handler.RecordEventStateChanged(withoutPayloads);
        _handler.RecordEventStateChanged(recorded, [payload]);

        var result = await _handler.CollectResult(_first);
        var next = await _handler.CollectResult(_first);

        result.States.Should().ContainSingle().Which.Should().BeSameAs(value);
        result.Events.Should().ContainSingle();
        result.Events[0].StreamId.Should().Be("recorded");
        result.Events[0].Events.Should().ContainSingle().Which.Should().BeSameAs(payload);
        next.Sequence.Should().BeGreaterThan(result.Sequence);
    }

    [Fact]
    public async Task CollectResult_NotTheHolder_Throws()
    {
        var collect = () => _handler.CollectResult(_first);

        await collect.Should().ThrowAsync<Exception>().WithMessage($"*'{_first}'*'{Guid.Empty}'*");
    }

    [Fact]
    public async Task OnSuccess_ConfirmsEveryoneDespiteThrows_ReleasesAndThrowsTheFailures()
    {
        var failingState = Substitute.For<IGrainStateTransactionParticipant>();
        failingState.When(x => x.OnTransactionSuccess()).Throw(new InvalidOperationException("state"));
        var state = Substitute.For<IGrainStateTransactionParticipant>();
        var failingEvents = Substitute.For<IGrainEventTransactionParticipant>();
        failingEvents.When(x => x.OnTransactionSuccess()).Throw(new InvalidOperationException("events"));
        var events = Substitute.For<IGrainEventTransactionParticipant>();

        await _handler.Join(_first);
        _handler.RecordStateChanged(failingState);
        _handler.RecordStateChanged(state);
        _handler.RecordEventStateChanged(failingEvents);
        _handler.RecordEventStateChanged(events);

        var confirm = () => _handler.OnSuccess(_first);

        var thrown = await confirm.Should().ThrowAsync<AggregateException>();
        thrown.Which.InnerExceptions.Select(e => e.Message).Should().BeEquivalentTo("state", "events");
        state.Received(1).OnTransactionSuccess();
        events.Received(1).OnTransactionSuccess();

        await _handler.Join(_second);
        (await _handler.CollectResult(_second)).States.Should().BeEmpty();
    }

    [Fact]
    public async Task OnFailure_RollsBackEveryoneAndReleases()
    {
        var state = Substitute.For<IGrainStateTransactionParticipant>();
        var events = Substitute.For<IGrainEventTransactionParticipant>();
        events.StreamId.Returns("stream");

        await _handler.Join(_first);
        _handler.RecordStateChanged(state);
        _handler.RecordEventStateChanged(events, [Payload()]);

        await _handler.OnFailure(_first);

        state.Received(1).OnTransactionFailure();
        events.Received(1).OnTransactionFailure();

        await _handler.Join(_second);
        var result = await _handler.CollectResult(_second);
        result.States.Should().BeEmpty();
        result.Events.Should().BeEmpty();
    }

    // A rollback that throws costs the others nothing. Unlike OnSuccess, where the memory equals the committed database,
    // the memory of the one that threw is unknown: the grain stays with the transaction, nobody reads that memory.
    [Fact]
    public async Task OnFailure_ParticipantThrows_TheOthersRollBackAndTheGrainStaysHeld()
    {
        var failing = Substitute.For<IGrainStateTransactionParticipant>();
        failing.When(x => x.OnTransactionFailure()).Throw(new InvalidOperationException("rollback"));
        var state = Substitute.For<IGrainStateTransactionParticipant>();
        var events = Substitute.For<IGrainEventTransactionParticipant>();

        await _handler.Join(_first);
        _handler.RecordStateChanged(failing);
        _handler.RecordStateChanged(state);
        _handler.RecordEventStateChanged(events);

        var error = await Record.ExceptionAsync(() => _handler.OnFailure(_first));

        state.Received(1).OnTransactionFailure();
        events.Received(1).OnTransactionFailure();

        error.Should().BeOfType<AggregateException>()
             .Which.InnerExceptions.Should().ContainSingle().Which.Message.Should().Be("rollback");

        var join = () => _handler.Join(_second);
        await join.Should().ThrowAsync<Exception>().WithMessage($"*'{_second}'*'{_first}'*");
    }

    // The takeover is the second attempt, for the one that threw only; a success gives the grain away.
    [Fact]
    public async Task Takeover_AfterAFailedRollback_RetriesTheOneThatThrewAndTakesTheGrain()
    {
        var attempts = 0;
        var failing = Substitute.For<IGrainStateTransactionParticipant>();

        failing.When(x => x.OnTransactionFailure()).Do(_ => {
            if (++attempts == 1)
                throw new InvalidOperationException("rollback");
        });

        var state = Substitute.For<IGrainStateTransactionParticipant>();

        await _handler.Join(_first);
        _handler.RecordStateChanged(failing);
        _handler.RecordStateChanged(state);

        var rollback = () => _handler.OnFailure(_first);
        await rollback.Should().ThrowAsync<AggregateException>();

        _options = _stuckAtOnce;
        await _handler.Join(_second);

        attempts.Should().Be(2);
        state.Received(1).OnTransactionFailure();
        await _handler.Seal(_second);
    }

    // A rollback that never succeeds: every transaction on the grain fails with the reason, none reads the memory.
    [Fact]
    public async Task Takeover_RollbackKeepsThrowing_JoinFailsAndTheGrainIsNotTaken()
    {
        var failing = Substitute.For<IGrainStateTransactionParticipant>();
        failing.When(x => x.OnTransactionFailure()).Throw(new InvalidOperationException("rollback"));

        await _handler.Join(_first);
        _handler.RecordStateChanged(failing);

        _options = _stuckAtOnce;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var join = () => _handler.Join(_second);
            await join.Should().ThrowAsync<AggregateException>();
        }

        var seal = () => _handler.Seal(_second);
        await seal.Should().ThrowAsync<Exception>().WithMessage($"*'{_second}'*'{_first}'*");
    }

    // The lock is given to a waiter by the release, the waiter takes the grain in a later turn: in between the grain
    // looks free and a new transaction must not start a wait without a bound there.
    [Fact]
    public async Task Join_WhileTheLockPassesToAWaiter_TheWaitIsBounded()
    {
        var turns = new ManualTurns();
        var previous = SynchronizationContext.Current;
        Task<Guid> waiter;
        Task<Guid> late;

        SynchronizationContext.SetSynchronizationContext(turns);

        try
        {
            _handler.Join(_first).IsCompletedSuccessfully.Should().BeTrue();

            _options = _longWait;
            waiter = _handler.Join(_second);

            _options = _noWait;
            _handler.OnSuccess(_first).IsCompletedSuccessfully.Should().BeTrue();
            late = _handler.Join(_third);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        var deadline = Stopwatch.StartNew();

        while (late.IsCompleted == false && deadline.Elapsed < TimeSpan.FromSeconds(2))
        {
            turns.RunPending();
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        late.IsCompleted.Should().BeTrue("the wait for a grain is bounded by LockWaitSeconds");

        var join = () => late;
        await join.Should().ThrowAsync<Exception>().WithMessage($"*'{_third}'*'{_second}'*");
        await waiter;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SealAndRejoin_RefreshTheGrace(bool seal)
    {
        var grace = TimeSpan.FromSeconds(0.3);

        _options = new TransactionOptions { LockWaitSeconds = 0.01f, StuckGraceSeconds = (float)grace.TotalSeconds };
        await _handler.Join(_first);
        await Task.Delay(grace + TimeSpan.FromSeconds(0.05), TestContext.Current.CancellationToken);

        await (seal ? _handler.Seal(_first) : _handler.Join(_first));
        var sinceRefresh = Stopwatch.StartNew();

        var isTakenOver = true;

        try
        {
            await _handler.Join(_second);
        }
        catch (Exception e) when (e.Message.StartsWith("Handler failed to join"))
        {
            isTakenOver = false;
        }

        // A takeover is legal only when the machine really stalled for the grace counted from the refresh.
        if (isTakenOver == true)
            sinceRefresh.Elapsed.Should().BeGreaterThan(grace);
    }

    private static EventPayload Payload()
    {
        return new EventPayload { Type = "type", Json = "{}" };
    }

    // The single-threaded scheduler of a grain, by hand: a continuation runs when the test says so.
    private sealed class ManualTurns : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _pending = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            _pending.Enqueue((d, state));
        }

        public void RunPending()
        {
            while (_pending.TryDequeue(out var turn))
                turn.Callback(turn.State);
        }
    }
}
