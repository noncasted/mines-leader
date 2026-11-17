using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State;

// The takeover of GrainTransactionOwner against the other turns of the grain. The calls of the handler run on a manual
// scheduler, one turn at a time as on an activation: the test decides what comes between the end of a lock wait and
// the turn that reacts to it.
public class GrainTransactionTakeoverOrderTests
{
    public GrainTransactionTakeoverOrderTests()
    {
        var config = Substitute.For<ITransactionConfig>();
        config.Value.Returns(_ => _options);
        _handler = new GrainTransactionHandler(config, NullLogger<GrainTransactionHandler>.Instance);
    }

    private static readonly TransactionOptions _startsNow = new() { LockWaitSeconds = 0.01f, StuckGraceSeconds = 600f };
    private static readonly TransactionOptions _runsOutSoon = new() { LockWaitSeconds = 0.01f, StuckGraceSeconds = 0f };
    private static readonly TransactionOptions _keepsWaiting = new() { LockWaitSeconds = 60f, StuckGraceSeconds = 600f };

    private readonly GrainTurns _turns = new();
    private readonly GrainTransactionHandler _handler;
    private readonly Guid _stuck = Guid.NewGuid();
    private readonly Guid _takingOver = Guid.NewGuid();
    private readonly Guid _waiting = Guid.NewGuid();
    private TransactionOptions _options = _startsNow;

    // The stuck transaction ends right after the takeover has freed the lock. With a wait inside the takeover, two
    // waiters of one transaction took both grants and the takeover stayed behind them, with nothing to bound its wait.
    [Fact]
    public async Task Takeover_StuckEndsAtThatMoment_TwoJoinsOfOneTransactionWaiting_TakeoverIsNotLeftWaiting()
    {
        await HoldTheGrain();

        var takeover = JoinAfterItsWaitRunsOut();

        _options = _keepsWaiting;
        Join(_waiting).NoObserve();
        Join(_waiting).NoObserve();

        await TheLockWaitOfTheTakeoverRanOut();
        OnSuccess(_stuck).NoObserve();

        await RunTurnsUntil(takeover);

        await takeover;
    }

    // The stuck one has just released and the lock is on its way to the waiter: nothing is stuck any more, the join whose
    // wait ran out at that moment fails and takes nothing from the waiter.
    [Fact]
    public async Task WaitRunsOut_LockIsOnItsWayToAWaiter_NoTakeoverAndTheWaiterGetsTheGrain()
    {
        await HoldTheGrain();

        var late = JoinAfterItsWaitRunsOut();

        _options = _keepsWaiting;
        var waiter = Join(_waiting);

        await TheLockWaitOfTheTakeoverRanOut();

        OnSuccess(_stuck).NoObserve();
        _turns.RunLast();
        _turns.RunFirst();

        await RunTurnsUntil(late, waiter);

        await waiter;
        await _handler.Seal(_waiting);

        var awaitLate = () => late;
        await awaitLate.Should().ThrowAsync<Exception>().WithMessage($"*'{_takingOver}'*");

        // One holder of the lock: the next one does not get in beside the waiter.
        _options = _startsNow;
        var next = () => _handler.Join(Guid.NewGuid());
        await next.Should().ThrowAsync<Exception>().WithMessage($"*'{_waiting}'*");
    }

    // The holder has released and nobody waits: the grain is free, the join whose wait ran out at that moment takes it.
    [Fact]
    public async Task WaitRunsOut_HolderHasJustReleased_NobodyWaits_TheGrainIsTaken()
    {
        await HoldTheGrain();

        var late = JoinAfterItsWaitRunsOut();

        await TheLockWaitOfTheTakeoverRanOut();

        OnSuccess(_stuck).NoObserve();
        _turns.RunLast();
        _turns.RunFirst();

        await RunTurnsUntil(late);

        await late;
        await _handler.Seal(_takingOver);
    }

    // Two parallel calls of one transaction wait for a busy grain. The lock comes once: the second call is on the grain
    // with the first, it does not wait for its own transaction until the wait runs out.
    [Fact]
    public async Task TwoJoinsOfOneTransaction_TheLockComes_BothAreOnTheGrain()
    {
        await HoldTheGrain();

        _options = _keepsWaiting;
        var first = Join(_waiting);

        _options = _startsNow;
        var second = Join(_waiting);

        OnSuccess(_stuck).NoObserve();

        await RunTurnsUntil(first, second);

        (await second).Should().Be(await first);
        await _handler.Seal(_waiting);
    }

    // The transaction is over before its second call had the lock: the grain is free, the second call does not take it
    // for a transaction nobody will end.
    [Fact]
    public async Task TwoJoinsOfOneTransaction_TheTransactionEnds_TheGrainStaysFree()
    {
        await HoldTheGrain();

        _options = _keepsWaiting;
        var first = Join(_waiting);
        var second = Join(_waiting);

        OnSuccess(_stuck).NoObserve();
        await RunTurnsUntil(first);

        OnSuccess(_waiting).NoObserve();
        await RunTurnsUntil(second);

        _options = _startsNow;
        var next = Guid.NewGuid();
        await _handler.Join(next);
        await _handler.Seal(next);
    }

    // The holder stays past the wait: both calls fail with the one wait, and it is not left behind for a next call.
    [Fact]
    public async Task TwoJoinsOfOneTransaction_TheWaitRunsOut_BothFailAndTheNextJoinWaitsAnew()
    {
        await HoldTheGrain();

        _options = _startsNow;
        var first = Join(_waiting);
        var second = Join(_waiting);

        await RunTurnsUntil(first, second);

        first.IsFaulted.Should().BeTrue();
        second.IsFaulted.Should().BeTrue();
        await _handler.Seal(_stuck);

        await _handler.OnSuccess(_stuck);
        await _handler.Join(_waiting);
        await _handler.Seal(_waiting);
    }

    // The first call has the grain and has changed the state when the wait of the second runs out past the grace: the
    // transaction is not stuck behind itself and its changes stay.
    [Fact]
    public async Task TwoJoinsOfOneTransaction_TheWaitRunsOutPastTheGrace_NoTakeoverOfItself()
    {
        await HoldTheGrain();

        _options = _keepsWaiting;
        var first = Join(_waiting);

        // The lock is on its way to the first call when the second comes.
        OnSuccess(_stuck).NoObserve();
        _turns.RunFirst();

        _options = _runsOutSoon;
        var second = Join(_waiting);

        // The first call has taken the grain.
        _turns.RunFirst();

        var state = Substitute.For<IGrainStateTransactionParticipant>();
        _handler.RecordStateChanged(state);

        await RunTurnsUntil(first, second);
        await first;
        await second;

        state.DidNotReceive().OnTransactionFailure();
        (await _handler.CollectResult(_waiting)).States.Should().ContainSingle();
    }

    private Task RunTurnsUntil(params Task[] calls)
    {
        return TestWait.Until(() => {
            _turns.Drain();
            return calls.All(call => call.IsCompleted);
        });
    }

    private async Task HoldTheGrain()
    {
        await Join(_stuck);
    }

    private Task<Guid> JoinAfterItsWaitRunsOut()
    {
        _options = _runsOutSoon;
        return Join(_takingOver);
    }

    private Task TheLockWaitOfTheTakeoverRanOut()
    {
        return TestWait.Until(() => _turns.Count == 1);
    }

    private Task<Guid> Join(Guid transactionId)
    {
        return _turns.Start(() => _handler.Join(transactionId));
    }

    // Comes to the activation behind the turns already queued: the test runs it.
    private Task<int> OnSuccess(Guid transactionId)
    {
        return _turns.Enqueue(async () => {
            await _handler.OnSuccess(transactionId);
            return 0;
        });
    }

    // The turns of one activation, run only by the test.
    private sealed class GrainTurns : TaskScheduler
    {
        private readonly List<Task> _tasks = new();

        public int Count
        {
            get
            {
                lock (_tasks)
                    return _tasks.Count;
            }
        }

        // The first turn of the call runs here and nothing else does: a wait that has run out stays queued for the test.
        public Task<T> Start<T>(Func<Task<T>> call)
        {
            var first = Task.Factory.StartNew(call, CancellationToken.None, TaskCreationOptions.None, this);
            Run(_ => first);
            return first.Unwrap();
        }

        public Task<T> Enqueue<T>(Func<Task<T>> call)
        {
            return Task.Factory.StartNew(call, CancellationToken.None, TaskCreationOptions.None, this).Unwrap();
        }

        public void Drain()
        {
            while (RunFirst() == true)
            {
            }
        }

        public bool RunFirst()
        {
            return Run(tasks => tasks[0]);
        }

        public bool RunLast()
        {
            return Run(tasks => tasks[^1]);
        }

        private bool Run(Func<List<Task>, Task> choose)
        {
            Task task;

            lock (_tasks)
            {
                if (_tasks.Count == 0)
                    return false;

                task = choose(_tasks);
                _tasks.Remove(task);
            }

            // The continuations of the turn come back here, not to the context of the test.
            var context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                TryExecuteTask(task);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
            }

            return true;
        }

        protected override void QueueTask(Task task)
        {
            lock (_tasks)
                _tasks.Add(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            lock (_tasks)
                return _tasks.ToArray();
        }
    }
}
