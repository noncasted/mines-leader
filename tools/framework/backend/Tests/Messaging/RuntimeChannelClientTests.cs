using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.State.Storages;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using static Framework.Tests.Messaging.MessagingClientStubs;

namespace Framework.Tests.Messaging;

// The client side of a channel without a cluster: what a resubscribe does with a catch-up, the local handlers of an
// observer, the failures of the transport and the resubscribe loop.
public class RuntimeChannelClientTests : LifetimeTest
{
    private readonly IRuntimeChannel _channel = Substitute.For<IRuntimeChannel>();
    private readonly RecordingLogger<RuntimeChannelClient> _logger = new();
    private readonly List<IGrainObserver> _observers = new();
    private readonly ChannelPosition _position = new() { Epoch = Guid.NewGuid() };

    private IOrleans Orleans => _orleans ??= OrleansOf<IRuntimeChannel, IRuntimeChannelObserver>(_channel, _observers);

    private IOrleans? _orleans;

    public RuntimeChannelClientTests()
    {
        _channel.AddObserver(default, default!).ReturnsForAnyArgs(_position);
        _channel.CatchUp(default).ReturnsForAnyArgs(new CatchUpResult { Epoch = _position.Epoch });
    }

    private RuntimeChannelClient.Listener CreateListener(RuntimeChannelOptions? options = null)
    {
        return new RuntimeChannelClient.Listener(new RuntimeChannelId("channel"), _channel, Orleans, _logger,
            options ?? new RuntimeChannelOptions());
    }

    private RuntimeChannelClient CreateClient(RuntimeChannelOptions? options = null)
    {
        var config = Substitute.For<IRuntimeChannelConfig>();
        config.Value.Returns(options ?? new RuntimeChannelOptions());
        return new RuntimeChannelClient(Orleans, ServicesWith(config), _logger);
    }

    private static SequencedMessage Message(long sequence, Guid epoch, string payload)
    {
        return new SequencedMessage { Sequence = sequence, Epoch = epoch, Payload = payload };
    }

    [Fact]
    public async Task Resubscribe_GapInSameEpoch_CallsGapHandlersThenReplays()
    {
        var epoch = Guid.NewGuid();
        var listener = CreateListener();
        var events = new List<string>();

        listener.AddHandler(message => {
            events.Add((string)message);
            return Task.CompletedTask;
        }, () => events.Add("gap"));

        await listener.ObserverSource.Send(Message(5, epoch, "seq5"));

        _channel.CatchUp(5).Returns(new CatchUpResult
        {
            GapDetected = true,
            CurrentSequence = 10,
            Epoch = epoch,
            Messages = [Message(9, epoch, "seq9"), Message(10, epoch, "seq10")]
        });

        await listener.Resubscribe();

        events.Should().Equal("seq5", "gap", "seq9", "seq10");
        listener.ObserverSource.LastSeenSequence.Should().Be(10);
        _logger.Count(LogLevel.Warning, "Gap detected on channel").Should().Be(1);
    }

    [Fact]
    public async Task Resubscribe_NothingMissed_NoGapAndNothingReplayed()
    {
        var epoch = Guid.NewGuid();
        var listener = CreateListener();
        var received = new List<object>();
        var gaps = 0;

        listener.AddHandler(message => {
            received.Add(message);
            return Task.CompletedTask;
        }, () => gaps++);

        await listener.ObserverSource.Send(Message(5, epoch, "seq5"));

        _channel.CatchUp(5).Returns(new CatchUpResult { CurrentSequence = 5, Epoch = epoch });

        await listener.Resubscribe();

        gaps.Should().Be(0);
        received.Should().Equal("seq5");
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Resubscribe_NewEpoch_CatchesUpFromStartAndCallsGapHandlers()
    {
        var oldEpoch = Guid.NewGuid();
        var newEpoch = Guid.NewGuid();
        var listener = CreateListener();
        var events = new List<string>();

        listener.AddHandler(message => {
            events.Add((string)message);
            return Task.CompletedTask;
        }, () => events.Add("gap"));

        await listener.ObserverSource.Send(Message(5, oldEpoch, "old5"));

        _channel.CatchUp(5).Returns(new CatchUpResult { CurrentSequence = 1, Epoch = newEpoch });
        _channel.CatchUp(0).Returns(new CatchUpResult
        {
            CurrentSequence = 1,
            Epoch = newEpoch,
            Messages = [Message(1, newEpoch, "new1")]
        });

        await listener.Resubscribe();

        events.Should().Equal("old5", "gap", "new1");
        listener.ObserverSource.LastSeenEpoch.Should().Be(newEpoch);
        listener.ObserverSource.LastSeenSequence.Should().Be(1);
        _logger.Count(LogLevel.Warning, "reactivated").Should().Be(1);
    }

    [Fact]
    public async Task Resubscribe_ReactivatedDuringCatchUp_ThrowsAndStillDeliversBuffered()
    {
        var oldEpoch = Guid.NewGuid();
        var firstNewEpoch = Guid.NewGuid();
        var secondNewEpoch = Guid.NewGuid();
        var listener = CreateListener();
        var received = new List<object>();
        var gaps = 0;

        listener.AddHandler(message => {
            received.Add(message);
            return Task.CompletedTask;
        }, () => gaps++);

        await listener.ObserverSource.Send(Message(5, oldEpoch, "old5"));

        // A message of the new epoch comes while the observer binds: it is buffered until the end of the resubscribe.
        _channel.AddObserver(default, default!).ReturnsForAnyArgs(async _ => {
            await listener.ObserverSource.Send(Message(1, firstNewEpoch, "buffered"));
            return _position;
        });

        _channel.CatchUp(5).Returns(new CatchUpResult { CurrentSequence = 1, Epoch = firstNewEpoch });
        _channel.CatchUp(0).Returns(new CatchUpResult { CurrentSequence = 1, Epoch = secondNewEpoch });

        var act = () => listener.Resubscribe();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*reactivated again*");
        gaps.Should().Be(0);
        received.Should().Equal("old5", "buffered");
    }

    [Fact]
    public async Task Resubscribe_BindFails_EndsBufferingAndRethrows()
    {
        var listener = CreateListener();
        var received = new List<object>();

        listener.AddHandler(message => {
            received.Add(message);
            return Task.CompletedTask;
        }, null);

        _channel.AddObserver(default, default!).ReturnsForAnyArgs<Task<ChannelPosition>>(async _ => {
            await listener.ObserverSource.Send("buffered");
            throw new InvalidOperationException("bind failed");
        });

        var act = () => listener.Resubscribe();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("bind failed");
        received.Should().Equal("buffered");
    }

    [Fact]
    public async Task GapHandlerThrows_OtherGapHandlersRunAndResubscribeGoesOn()
    {
        var epoch = Guid.NewGuid();
        var listener = CreateListener();
        var received = new List<object>();
        var secondGap = 0;

        listener.AddHandler(message => {
            received.Add(message);
            return Task.CompletedTask;
        }, () => throw new InvalidOperationException("gap handler failed"));

        listener.AddHandler(_ => Task.CompletedTask, () => secondGap++);

        await listener.ObserverSource.Send(Message(5, epoch, "seq5"));

        _channel.CatchUp(5).Returns(new CatchUpResult
        {
            GapDetected = true,
            CurrentSequence = 7,
            Epoch = epoch,
            Messages = [Message(7, epoch, "seq7")]
        });

        await listener.Resubscribe();

        secondGap.Should().Be(1);
        received.Should().Equal("seq5", "seq7");
        _logger.Count(LogLevel.Error, "Gap handler failed for channel channel").Should().Be(1);
    }

    [Fact]
    public async Task RemovedHandler_GetsNoMessageAndNoGap()
    {
        var epoch = Guid.NewGuid();
        var listener = CreateListener();
        var removedCalls = 0;
        var remaining = new List<object>();

        var removed = listener.AddHandler(_ => {
            removedCalls++;
            return Task.CompletedTask;
        }, () => removedCalls++);

        var kept = listener.AddHandler(message => {
            remaining.Add(message);
            return Task.CompletedTask;
        }, null);

        listener.RemoveHandler(removed).Should().BeFalse();

        await listener.ObserverSource.Send(Message(5, epoch, "seq5"));

        _channel.CatchUp(5).Returns(new CatchUpResult { GapDetected = true, CurrentSequence = 7, Epoch = epoch });

        await listener.Resubscribe();

        removedCalls.Should().Be(0);
        remaining.Should().Equal("seq5");
        listener.RemoveHandler(kept).Should().BeTrue();
    }

    [Fact]
    public async Task Deliver_NoHandlers_Throws()
    {
        var listener = CreateListener();

        var act = () => listener.ObserverSource.Send("message");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("No local channel handlers for channel");
    }

    [Fact]
    public async Task Deliver_OneHandlerFails_OtherGetsMessageAndCallSucceeds()
    {
        var listener = CreateListener();
        var received = new List<object>();

        listener.AddHandler(_ => throw new InvalidOperationException("handler failed"), null);

        listener.AddHandler(message => {
            received.Add(message);
            return Task.CompletedTask;
        }, null);

        await listener.ObserverSource.Send("message");

        received.Should().Equal("message");
        _logger.Count(LogLevel.Error, "failed for channel channel").Should().Be(1);
    }

    [Fact]
    public async Task Deliver_AllHandlersFail_ThrowsAggregate()
    {
        var listener = CreateListener();

        listener.AddHandler(_ => throw new InvalidOperationException("first"), null);
        listener.AddHandler(_ => throw new InvalidOperationException("second"), null);

        var act = () => listener.ObserverSource.Send("message");

        var failure = await act.Should().ThrowAsync<AggregateException>()
                               .WithMessage("All local channel handlers failed for channel*");

        failure.Which.InnerExceptions.Should().HaveCount(2);
    }

    [Fact]
    public void RecordFailure_LogsFirstAndEveryTenth_SuccessStartsAgain()
    {
        var listener = CreateListener();

        for (var i = 0; i < 20; i++)
            listener.RecordFailure(new InvalidOperationException("bind failed"));

        var logged = _logger.Entries.Select(entry => entry.Message).ToList();

        logged.Should().HaveCount(3);
        logged[0].Should().Contain("(attempt 1)").And.Contain("to channel channel");
        logged[1].Should().Contain("(attempt 10)");
        logged[2].Should().Contain("(attempt 20)");

        listener.RecordSuccess();
        listener.RecordFailure(new InvalidOperationException("bind failed"));

        _logger.Entries.Should().HaveCount(4);
        _logger.Entries[3].Message.Should().Contain("(attempt 1)");
    }

    [Fact]
    public void Interval_FollowsTheOptionsAndTheOutcomes()
    {
        var listener = CreateListener(new RuntimeChannelOptions
        {
            ResubscribeMinIntervalMs = 2_000,
            ResubscribeMaxIntervalMs = 2_000
        });

        listener.Interval.GetNextDelay().Should().BeCloseTo(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(401));

        listener.RecordFailure(new InvalidOperationException("bind failed"));
        listener.Interval.GetNextDelay().Should().BeCloseTo(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(201));

        listener.RecordSuccess();
        listener.Interval.GetNextDelay().Should().BeCloseTo(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(401));
    }

    [Fact]
    public async Task Cleanup_RunsOnceAndRemovesObserverAndReference()
    {
        var listener = CreateListener();

        listener.Cleanup();
        listener.Cleanup();

        await TestWait.Until(() => Orleans.Client.ReceivedCalls()
                                   .Any(call => call.GetMethodInfo().Name == "DeleteObjectReference"));

        await _channel.Received(1).RemoveObserver(listener.ObserverSource.Id);
        Orleans.Client.Received(1).DeleteObjectReference<IRuntimeChannelObserver>(listener.ObserverReference);
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Cleanup_TransportFails_LogsAndStillDeletesReference()
    {
        var listener = CreateListener();

        _channel.RemoveObserver(listener.ObserverSource.Id).ThrowsAsync(new InvalidOperationException("remove failed"));

        Orleans.Client.When(client => client.DeleteObjectReference<IRuntimeChannelObserver>(listener.ObserverReference))
               .Do(_ => throw new InvalidOperationException("delete failed"));

        listener.Cleanup();

        await TestWait.Until(() => _logger.Entries.Count == 2);

        _logger.Count(LogLevel.Warning, "Failed to remove observer").Should().Be(1);
        _logger.Count(LogLevel.Warning, "Failed to delete object reference").Should().Be(1);
    }

    [Fact]
    public async Task Listen_TerminatedLifetime_DoesNothing()
    {
        var lifetime = TestLifetime.Child();
        lifetime.Terminate();

        await CreateClient().Listen<string>(lifetime, new RuntimeChannelId("channel"), _ => Task.CompletedTask);

        await _channel.DidNotReceiveWithAnyArgs().AddObserver(default, default!);
        _observers.Should().BeEmpty();
    }

    [Fact]
    public async Task Listen_BindFails_LogsRemovesListenerAndRethrows()
    {
        var lifetime = TestLifetime.Child();
        var client = CreateClient();

        _channel.AddObserver(default, default!).ThrowsAsyncForAnyArgs(new InvalidOperationException("bind failed"));

        var act = () => client.Listen<string>(lifetime, new RuntimeChannelId("channel"), _ => Task.CompletedTask);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("bind failed");
        _logger.Count(LogLevel.Error, "Failed to rebind observer").Should().Be(1);

        await TestWait.Until(() => _channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "RemoveObserver"));

        // The end of the lifetime finds nothing left to clean.
        lifetime.Terminate();
        await _channel.ReceivedWithAnyArgs(1).RemoveObserver(default);
    }

    [Fact]
    public async Task Listen_MessageOfAnotherType_FailsTheDelivery()
    {
        await CreateClient().Listen<string>(TestLifetime, new RuntimeChannelId("channel"), _ => Task.CompletedTask);

        var act = () => ((RuntimeChannelObserver)_observers.Single()).Send(42);

        var failure = await act.Should().ThrowAsync<AggregateException>();
        failure.Which.InnerExceptions.Single().Should().BeOfType<InvalidCastException>();
    }

    [Fact]
    public async Task Listen_EveryCallHasItsOwnObserver_LifetimeRemovesOnlyItsOwn()
    {
        var client = CreateClient();
        var firstLifetime = TestLifetime.Child();
        var id = new RuntimeChannelId("channel");

        await client.Listen<string>(firstLifetime, id, _ => Task.CompletedTask);
        await client.Listen<string>(TestLifetime, id, _ => Task.CompletedTask);

        _observers.Should().HaveCount(2);

        firstLifetime.Terminate();

        await TestWait.Until(() => _channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "RemoveObserver"));
        await _channel.Received(1).RemoveObserver(((RuntimeChannelObserver)_observers[0]).Id);
    }

    [Fact]
    public void Constructor_ResolvesNoConfig()
    {
        var services = Substitute.For<IServiceProvider>();

        _ = new RuntimeChannelClient(Orleans, services, _logger);

        services.DidNotReceiveWithAnyArgs().GetService(default!);
    }

    [Fact]
    public async Task Publish_GoesToTheChannel()
    {
        await CreateClient().Publish(new RuntimeChannelId("channel"), "message");

        await _channel.Received(1).Publish("message");
    }

    [Fact]
    public async Task Loop_NoListeners_SleepsTheMinInterval_PicksUpNewOne()
    {
        var lifetime = TestLifetime.Child();
        var client = CreateClient(new RuntimeChannelOptions { ResubscribeMinIntervalMs = 10, ResubscribeMaxIntervalMs = 10 });

        await client.Start(lifetime);
        await client.Listen<string>(lifetime, new RuntimeChannelId("channel"), _ => Task.CompletedTask);

        await TestWait.Until(() => _channel.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "AddObserver") >= 3);

        lifetime.Terminate();
    }

    [Fact]
    public async Task Loop_OneListenerFails_OtherGoesOn_LifetimeEndsIt()
    {
        var lifetime = TestLifetime.Child();
        var client = CreateClient(new RuntimeChannelOptions { ResubscribeMinIntervalMs = 10, ResubscribeMaxIntervalMs = 10 });
        var failing = new List<Guid>();
        var healthyCalls = 0;

        await client.Listen<string>(lifetime, new RuntimeChannelId("failing"), _ => Task.CompletedTask);
        await client.Listen<string>(lifetime, new RuntimeChannelId("healthy"), _ => Task.CompletedTask);

        var failingId = ((RuntimeChannelObserver)_observers[0]).Id;

        _channel.AddObserver(default, default!).ReturnsForAnyArgs(call => {
            if (call.Arg<Guid>() != failingId)
            {
                Interlocked.Increment(ref healthyCalls);
                return Task.FromResult(_position);
            }

            lock (failing)
                failing.Add(failingId);

            return Task.FromException<ChannelPosition>(new InvalidOperationException("bind failed"));
        });

        await client.Start(lifetime);

        // A failure makes the next delay about a second, and the shortest delay of the listeners is the sleep.
        await TestWait.Until(() => Volatile.Read(ref healthyCalls) >= 2);

        _logger.Count(LogLevel.Error, "(attempt 1)").Should().Be(1);

        // A window: the resubscribe loop gives no signal of its end, so only the absence of calls after it shows it ended.
        lifetime.Terminate();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        var callsAfterEnd = Volatile.Read(ref healthyCalls);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Volatile.Read(ref healthyCalls).Should().Be(callsAfterEnd);
    }

    [Fact]
    public void RecordFailure_FromSeveralThreads_CountsEveryFailure()
    {
        const int threads = 4;
        const int failuresPerThread = 200_000;

        var logger = new AttemptsLogger();
        var listener = new RuntimeChannelClient.Listener(new RuntimeChannelId("channel"), _channel, Orleans, logger,
            new RuntimeChannelOptions());
        var failure = new InvalidOperationException("bind failed");

        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ => {
            for (var i = 0; i < failuresPerThread; i++)
                listener.RecordFailure(failure);
        });

        logger.LastAttempt.Should().Be(threads * failuresPerThread);
    }

    [Fact]
    public async Task Loop_OneListenerHangs_OthersGoOn_TheHungOneIsNotCalledAgain()
    {
        var lifetime = TestLifetime.Child();
        var client = CreateClient(new RuntimeChannelOptions
        {
            ResubscribeMinIntervalMs = 10,
            ResubscribeMaxIntervalMs = 10,
            ResubscribeTimeoutMs = 50
        });

        await client.Listen<string>(lifetime, new RuntimeChannelId("hung"), _ => Task.CompletedTask);
        await client.Listen<string>(lifetime, new RuntimeChannelId("healthy"), _ => Task.CompletedTask);

        var hungId = ((RuntimeChannelObserver)_observers[0]).Id;
        var hang = new TaskCompletionSource<ChannelPosition>();
        var hungCalls = 0;
        var healthyCalls = 0;

        _channel.AddObserver(default, default!).ReturnsForAnyArgs(call => {
            if (call.Arg<Guid>() != hungId)
            {
                Interlocked.Increment(ref healthyCalls);
                return Task.FromResult(_position);
            }

            Interlocked.Increment(ref hungCalls);
            return hang.Task;
        });

        await client.Start(lifetime);

        await TestWait.Until(() => Volatile.Read(ref healthyCalls) >= 3);

        Volatile.Read(ref hungCalls).Should().Be(1);
        _logger.Count(LogLevel.Error, "(attempt 1)").Should().Be(1);

        lifetime.Terminate();
        hang.SetResult(_position);
    }

    private class AttemptsLogger : ILogger
    {
        private long _lastAttempt;

        public long LastAttempt => Interlocked.Read(ref _lastAttempt);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var attempt = ((IEnumerable<KeyValuePair<string, object?>>)state!).First(pair => pair.Key == "Count").Value;

            long current;

            do
                current = Interlocked.Read(ref _lastAttempt);
            while (Convert.ToInt64(attempt) > current
                   && Interlocked.CompareExchange(ref _lastAttempt, Convert.ToInt64(attempt), current) != current);
        }
    }
}
