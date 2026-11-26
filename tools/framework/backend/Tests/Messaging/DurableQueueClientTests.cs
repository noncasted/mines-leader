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

// The client side of a durable queue without a cluster: one listener of a queue with its local handlers, the
// failures of the transport, the resubscribe loop and the push without a transaction.
public class DurableQueueClientTests : LifetimeTest
{
    private readonly IDurableQueue _queue = Substitute.For<IDurableQueue>();
    private readonly ISideEffectsStorage _sideEffects = Substitute.For<ISideEffectsStorage>();
    private readonly RecordingLogger<DurableQueueClient> _logger = new();
    private readonly List<IGrainObserver> _observers = new();
    private readonly TestQueueId _id = new("queue");

    private IOrleans Orleans => _orleans ??= OrleansOf<IDurableQueue, IDurableQueueObserver>(_queue, _observers);

    private IOrleans? _orleans;

    private DurableQueueClient CreateClient()
    {
        return new DurableQueueClient(Orleans, _sideEffects, _logger);
    }

    private DurableQueueClient.Listener CreateListener()
    {
        return new DurableQueueClient.Listener(_id, _queue, Orleans, _logger);
    }

    [Fact]
    public async Task Listen_TerminatedLifetime_DoesNothing()
    {
        var lifetime = TestLifetime.Child();
        lifetime.Terminate();

        await CreateClient().Listen<string>(lifetime, _id, _ => Task.CompletedTask);

        await _queue.DidNotReceiveWithAnyArgs().AddObserver(default, default!);
        _observers.Should().BeEmpty();
    }

    [Fact]
    public async Task Listen_TwiceOnOneQueue_OneObserverWithBothHandlers()
    {
        var client = CreateClient();
        var first = new List<string>();
        var second = new List<string>();

        await client.Listen<string>(TestLifetime, _id, message => {
            first.Add(message);
            return Task.CompletedTask;
        });

        await client.Listen<string>(TestLifetime, _id, message => {
            second.Add(message);
            return Task.CompletedTask;
        });

        _observers.Should().HaveCount(1);
        await _queue.ReceivedWithAnyArgs(1).AddObserver(default, default!);

        await ((DurableQueueObserver)_observers.Single()).Send("message");

        first.Should().Equal("message");
        second.Should().Equal("message");
    }

    [Fact]
    public async Task Listen_ConcurrentlyOnOneQueue_OneObserver()
    {
        var client = CreateClient();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
            client.Listen<string>(TestLifetime, _id, _ => Task.CompletedTask))));

        _observers.Should().HaveCount(1);
    }

    [Fact]
    public async Task LifetimeEnd_OfLastHandler_RemovesTheObserver_ThenANewListenerBinds()
    {
        var client = CreateClient();
        var firstLifetime = TestLifetime.Child();
        var secondLifetime = TestLifetime.Child();

        await client.Listen<string>(firstLifetime, _id, _ => Task.CompletedTask);
        await client.Listen<string>(secondLifetime, _id, _ => Task.CompletedTask);

        firstLifetime.Terminate();
        await _queue.DidNotReceiveWithAnyArgs().RemoveObserver(default);

        secondLifetime.Terminate();

        await TestWait.Until(() => Orleans.Client.ReceivedCalls()
                                   .Any(call => call.GetMethodInfo().Name == "DeleteObjectReference"));

        await _queue.Received(1).RemoveObserver(((DurableQueueObserver)_observers[0]).Id);

        await client.Listen<string>(TestLifetime, _id, _ => Task.CompletedTask);

        _observers.Should().HaveCount(2);
    }

    [Fact]
    public async Task Listen_BindFails_LogsRemovesListenerAndRethrows()
    {
        var client = CreateClient();
        var lifetime = TestLifetime.Child();

        _queue.AddObserver(default, default!).ThrowsAsyncForAnyArgs(new InvalidOperationException("bind failed"));

        var act = () => client.Listen<string>(lifetime, _id, _ => Task.CompletedTask);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("bind failed");
        _logger.Count(LogLevel.Error, "Failed to rebind observer").Should().Be(1);

        await TestWait.Until(() => _queue.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "RemoveObserver"));

        // Gone from the client: the end of the lifetime cleans nothing, the next listen creates a new listener.
        lifetime.Terminate();
        await _queue.ReceivedWithAnyArgs(1).RemoveObserver(default);

        _queue.AddObserver(default, default!).ReturnsForAnyArgs(Task.CompletedTask);
        await client.Listen<string>(TestLifetime, _id, _ => Task.CompletedTask);

        _observers.Should().HaveCount(2);
    }

    [Fact]
    public async Task Listen_MessageOfAnotherType_FailsTheDelivery()
    {
        await CreateClient().Listen<string>(TestLifetime, _id, _ => Task.CompletedTask);

        var act = () => ((DurableQueueObserver)_observers.Single()).Send(42);

        var failure = await act.Should().ThrowAsync<AggregateException>();
        failure.Which.InnerExceptions.Single().Should().BeOfType<InvalidCastException>();
    }

    [Fact]
    public async Task Deliver_NoHandlers_Throws()
    {
        var act = () => CreateListener().ObserverSource.Send("message");

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("No local durable queue handlers for test-queue-queue");
    }

    [Fact]
    public async Task Deliver_OneHandlerFails_OtherGetsMessageAndCallSucceeds()
    {
        var listener = CreateListener();
        var received = new List<object>();

        listener.AddHandler(_ => throw new InvalidOperationException("handler failed"));

        listener.AddHandler(message => {
            received.Add(message);
            return Task.CompletedTask;
        });

        await listener.ObserverSource.Send("message");

        received.Should().Equal("message");
        _logger.Count(LogLevel.Error, "failed for queue test-queue-queue").Should().Be(1);
    }

    [Fact]
    public async Task Deliver_AllHandlersFail_ThrowsAggregate()
    {
        var listener = CreateListener();

        listener.AddHandler(_ => throw new InvalidOperationException("first"));
        listener.AddHandler(_ => throw new InvalidOperationException("second"));

        var act = () => listener.ObserverSource.Send("message");

        var failure = await act.Should().ThrowAsync<AggregateException>()
                               .WithMessage("All local durable queue handlers failed for test-queue-queue*");

        failure.Which.InnerExceptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Deliver_HandlersOneByOne_InTheOrderOfAdding()
    {
        var listener = CreateListener();
        var events = new List<string>();
        var firstMayEnd = new TaskCompletionSource();

        listener.AddHandler(async _ => {
            events.Add("first started");
            await firstMayEnd.Task;
            events.Add("first ended");
        });

        listener.AddHandler(_ => {
            events.Add("second");
            return Task.CompletedTask;
        });

        var delivery = listener.ObserverSource.Send("message");

        events.Should().Equal("first started");

        firstMayEnd.SetResult();
        await delivery;

        events.Should().Equal("first started", "first ended", "second");
    }

    [Fact]
    public void RemoveHandler_TellsWhenTheLastIsGone()
    {
        var listener = CreateListener();
        var first = listener.AddHandler(_ => Task.CompletedTask)!.Value;
        var second = listener.AddHandler(_ => Task.CompletedTask)!.Value;

        listener.RemoveHandler(first).Should().BeFalse();
        listener.RemoveHandler(second).Should().BeTrue();

        listener.AddHandler(_ => Task.CompletedTask).Should().BeNull("the listener is on its way out");
    }

    [Fact]
    public void RecordFailure_LogsFirstAndEveryTenth_SuccessStartsAgain()
    {
        var listener = CreateListener();

        for (var i = 0; i < 10; i++)
            listener.RecordFailure(new InvalidOperationException("bind failed"));

        _logger.Count(LogLevel.Error, "(attempt 1) to queue test-queue-queue").Should().Be(1);
        _logger.Count(LogLevel.Error, "(attempt 10) to queue test-queue-queue").Should().Be(1);
        _logger.Entries.Should().HaveCount(2);

        listener.RecordSuccess();
        listener.RecordFailure(new InvalidOperationException("bind failed"));

        _logger.Count(LogLevel.Error, "(attempt 1) to queue test-queue-queue").Should().Be(2);
    }

    [Fact]
    public void Interval_FailureMakesTheNextDelayShort()
    {
        var listener = CreateListener();

        listener.Interval.GetNextDelay().Should().BeCloseTo(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2.01));

        listener.RecordFailure(new InvalidOperationException("bind failed"));

        listener.Interval.GetNextDelay().Should().BeCloseTo(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(201));
    }

    [Fact]
    public async Task Cleanup_RunsOnce()
    {
        var listener = CreateListener();

        listener.Cleanup();
        listener.Cleanup();

        await TestWait.Until(() => Orleans.Client.ReceivedCalls()
                                   .Any(call => call.GetMethodInfo().Name == "DeleteObjectReference"));

        await _queue.Received(1).RemoveObserver(listener.ObserverSource.Id);
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Cleanup_TransportFails_LogsAndStillDeletesReference()
    {
        var listener = CreateListener();

        _queue.RemoveObserver(listener.ObserverSource.Id).ThrowsAsync(new InvalidOperationException("remove failed"));

        Orleans.Client.When(client => client.DeleteObjectReference<IDurableQueueObserver>(listener.ObserverReference))
               .Do(_ => throw new InvalidOperationException("delete failed"));

        listener.Cleanup();

        await TestWait.Until(() => _logger.Entries.Count == 2);

        _logger.Count(LogLevel.Warning, "Failed to remove observer").Should().Be(1);
        _logger.Count(LogLevel.Warning, "Failed to delete object reference").Should().Be(1);
    }

    [Fact]
    public async Task Loop_BindFails_LogsAndBindsAgainAfterAboutASecond()
    {
        var lifetime = TestLifetime.Child();
        var client = CreateClient();
        var calls = 0;

        await client.Listen<string>(lifetime, _id, _ => Task.CompletedTask);

        _queue.AddObserver(default, default!).ReturnsForAnyArgs(_ => Interlocked.Increment(ref calls) == 1
            ? Task.FromException(new InvalidOperationException("bind failed"))
            : Task.CompletedTask);

        await client.Start(lifetime);

        await TestWait.Until(() => Volatile.Read(ref calls) >= 2);

        _logger.Count(LogLevel.Error, "(attempt 1) to queue test-queue-queue").Should().Be(1);

        lifetime.Terminate();
    }

    [Fact]
    public async Task PushDirect_WritesTheSideEffect()
    {
        await CreateClient().PushDirect(_id, "message");

        await _sideEffects.Received(1).Write(Arg.Is<DurableQueueSideEffect>(effect =>
            effect.QueueName == "test-queue-queue" && (string)effect.Message == "message" && effect.CorrelationId != Guid.Empty));
    }

    [Fact]
    public async Task Listen_WhileTheLastHandlerGoes_TheNewHandlerStaysReachable()
    {
        var client = CreateClient();
        var removed = new HashSet<Guid>();

        _queue.RemoveObserver(default).ReturnsForAnyArgs(call => {
            lock (removed)
                removed.Add(call.Arg<Guid>());

            return Task.CompletedTask;
        });

        for (var i = 0; i < 3_000; i++)
        {
            var leaving = TestLifetime.Child();
            var staying = TestLifetime.Child();
            var received = 0;

            await client.Listen<string>(leaving, _id, _ => Task.CompletedTask);

            await Race(() => {
                leaving.Terminate();
                return Task.CompletedTask;
            }, () => client.Listen<string>(staying, _id, _ => {
                received++;
                return Task.CompletedTask;
            }));

            foreach (var observer in LiveObservers(removed))
                await observer.Send("message");

            received.Should().Be(1, $"a listen that returned gets the messages (iteration {i})");

            staying.Terminate();
        }
    }

    private List<DurableQueueObserver> LiveObservers(HashSet<Guid> removed)
    {
        lock (_observers)
        lock (removed)
            return _observers.Cast<DurableQueueObserver>().Where(observer => removed.Contains(observer.Id) == false).ToList();
    }

    [Fact]
    public void PushTransactional_OutsideTransaction_TellsWhatIsWrong()
    {
        var act = () => CreateClient().PushTransactional(_id, "message");

        act.Should().Throw<InvalidOperationException>().WithMessage("*test-queue-queue*outside of a transaction*");
    }
}
