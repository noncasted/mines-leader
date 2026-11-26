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

// The client side of a pipe without a cluster: the repeats of a send, the binding of a handler, the failures of the
// transport and the resubscribe loop.
public class RuntimePipeClientTests : LifetimeTest
{
    private readonly IRuntimePipe _pipe = Substitute.For<IRuntimePipe>();
    private readonly RecordingLogger<RuntimePipeClient> _logger = new();
    private readonly List<IGrainObserver> _observers = new();
    private readonly TestPipeId _id = new("pipe");

    private IOrleans Orleans => _orleans ??= OrleansOf<IRuntimePipe, IRuntimePipeObserver>(_pipe, _observers);

    private IOrleans? _orleans;

    private RuntimePipeClient CreateClient(int retryCount = 2, int baseDelayMs = 10)
    {
        var config = Substitute.For<IRuntimePipeConfig>();
        config.Value.Returns(new RuntimePipeOptions { SendRetryCount = retryCount, SendRetryBaseDelayMs = baseDelayMs });
        return new RuntimePipeClient(Orleans, ServicesWith(config), _logger);
    }

    private static InvalidOperationException NoObserver()
    {
        return new InvalidOperationException($"{RuntimePipe.NoObserverPrefix} to pipe");
    }

    [Fact]
    public async Task Send_NoObserver_RepeatsWithGrowingDelayThenThrowsLast()
    {
        var calls = 0;

        _pipe.Send<string>("message").Returns<Task<string>>(_ => {
            calls++;
            return Task.FromException<string>(new InvalidOperationException($"{RuntimePipe.NoObserverPrefix} call {calls}"));
        });

        var act = () => CreateClient().Send<string>(_id, "message");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*call 3");

        calls.Should().Be(3);

        var logged = _logger.Entries.Select(entry => entry.Message).ToList();

        logged.Should().HaveCount(2);
        logged[0].Should().Contain("(attempt 1/3)").And.Contain("retrying in 10ms");
        logged[1].Should().Contain("(attempt 2/3)").And.Contain("retrying in 20ms");
    }

    [Fact]
    public async Task Send_NoObserverInInnerException_RepeatsAndReturnsTheAnswer()
    {
        var calls = 0;

        _pipe.Send<string>("message").Returns<Task<string>>(_ => {
            calls++;

            return calls == 1
                ? Task.FromException<string>(new AggregateException("outer", NoObserver()))
                : Task.FromResult("answer");
        });

        var answer = await CreateClient().Send<string>(_id, "message");

        answer.Should().Be("answer");
        calls.Should().Be(2);
    }

    [Fact]
    public async Task Send_OtherFailure_NotRepeated()
    {
        _pipe.Send<string>("message").ThrowsAsync(new TimeoutException("the handler may have run"));

        var act = () => CreateClient().Send<string>(_id, "message");

        await act.Should().ThrowAsync<TimeoutException>();
        await _pipe.Received(1).Send<string>("message");
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_NoRetries_OneCallAndNoLog()
    {
        _pipe.Send<string>("message").ThrowsAsync(NoObserver());

        var act = () => CreateClient(retryCount: 0).Send<string>(_id, "message");

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _pipe.Received(1).Send<string>("message");
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ResolvesNoConfig()
    {
        var services = Substitute.For<IServiceProvider>();

        _ = new RuntimePipeClient(Orleans, services, _logger);

        services.DidNotReceiveWithAnyArgs().GetService(default!);
    }

    [Fact]
    public async Task Exists_AsksThePipe()
    {
        _pipe.HasObserver().Returns(true);

        (await CreateClient().Exists(_id)).Should().BeTrue();
    }

    [Fact]
    public async Task Exists_PipeFails_TheFailureGoesToTheCaller()
    {
        _pipe.HasObserver().ThrowsAsync(new InvalidOperationException("no pipe"));

        var act = () => CreateClient().Exists(_id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("no pipe");
    }

    [Fact]
    public async Task AddHandler_TerminatedLifetime_DoesNothing()
    {
        var lifetime = TestLifetime.Child();
        lifetime.Terminate();

        await CreateClient().AddHandler<string, string>(lifetime, _id, Task.FromResult);

        await _pipe.DidNotReceiveWithAnyArgs().BindObserver(default, default!);
        _observers.Should().BeEmpty();
    }

    [Fact]
    public async Task AddHandler_RequestOfAnotherType_FailsWithCast()
    {
        await CreateClient().AddHandler<string, string>(TestLifetime, _id, Task.FromResult);

        var act = () => ((RuntimePipeObserver)_observers.Single()).Send<string>(42);

        await act.Should().ThrowAsync<InvalidCastException>().WithMessage("Expected System.String*");
    }

    [Fact]
    public async Task AddHandler_NullResponse_FailsWithCast()
    {
        await CreateClient().AddHandler<string, string>(TestLifetime, _id, _ => Task.FromResult<string>(null!));

        var act = () => ((RuntimePipeObserver)_observers.Single()).Send<string>("request");

        await act.Should().ThrowAsync<InvalidCastException>().WithMessage("*got null");
    }

    [Fact]
    public async Task AddHandler_SecondOfOnePipe_ReplacesTheFirst()
    {
        var client = CreateClient();
        var bound = new List<Guid>();

        _pipe.BindObserver(default, default!).ReturnsForAnyArgs(call => {
            bound.Add(call.Arg<Guid>());
            return Task.CompletedTask;
        });

        await client.AddHandler<string, string>(TestLifetime, _id, Task.FromResult);
        await client.AddHandler<string, string>(TestLifetime, _id, Task.FromResult);

        bound.Should().HaveCount(2);
        _logger.Count(LogLevel.Warning, "Removing duplicate observer").Should().Be(1);

        await TestWait.Until(() => _pipe.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "UnbindObserver"));
        await _pipe.Received(1).UnbindObserver(bound[0]);

        // The first observer has no handler any more.
        var act = () => ((RuntimePipeObserver)_observers[0]).Ping();
        await act.Should().ThrowAsync<InvalidOperationException>();
        await ((RuntimePipeObserver)_observers[1]).Ping();
    }

    [Fact]
    public async Task AddHandler_AnotherPipe_KeepsTheFirst()
    {
        var client = CreateClient();

        await client.AddHandler<string, string>(TestLifetime, _id, Task.FromResult);
        await client.AddHandler<string, string>(TestLifetime, new TestPipeId("other"), Task.FromResult);

        _logger.Count(LogLevel.Warning, "Removing duplicate observer").Should().Be(0);
        await _pipe.DidNotReceiveWithAnyArgs().UnbindObserver(default);
    }

    [Fact]
    public async Task AddHandler_BindFails_LogsRemovesListenerAndRethrows()
    {
        var client = CreateClient();
        var lifetime = TestLifetime.Child();

        _pipe.BindObserver(default, default!).ThrowsAsyncForAnyArgs(new InvalidOperationException("bind failed"));

        var act = () => client.AddHandler<string, string>(lifetime, _id, Task.FromResult);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("bind failed");
        _logger.Count(LogLevel.Error, "Failed to rebind observer").Should().Be(1);

        await TestWait.Until(() => _pipe.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "UnbindObserver"));

        // Gone from the client: the end of the lifetime cleans nothing, the next handler replaces nothing.
        lifetime.Terminate();
        await _pipe.ReceivedWithAnyArgs(1).UnbindObserver(default);

        _pipe.BindObserver(default, default!).ReturnsForAnyArgs(Task.CompletedTask);
        await client.AddHandler<string, string>(TestLifetime, _id, Task.FromResult);
        _logger.Count(LogLevel.Warning, "Removing duplicate observer").Should().Be(0);
    }

    [Fact]
    public async Task LifetimeEnd_UnbindsClearsHandlerAndDeletesReference()
    {
        var lifetime = TestLifetime.Child();

        await CreateClient().AddHandler<string, string>(lifetime, _id, Task.FromResult);

        lifetime.Terminate();

        await TestWait.Until(() => Orleans.Client.ReceivedCalls()
                                   .Any(call => call.GetMethodInfo().Name == "DeleteObjectReference"));

        await _pipe.ReceivedWithAnyArgs(1).UnbindObserver(default);

        var act = () => ((RuntimePipeObserver)_observers.Single()).Ping();
        await act.Should().ThrowAsync<InvalidOperationException>();

        _logger.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task LifetimeEnd_TransportFails_LogsAndStillDeletesReference()
    {
        var lifetime = TestLifetime.Child();

        _pipe.UnbindObserver(default).ThrowsAsyncForAnyArgs(new InvalidOperationException("unbind failed"));

        Orleans.Client.WhenForAnyArgs(client => client.DeleteObjectReference<IRuntimePipeObserver>(default!))
               .Do(_ => throw new InvalidOperationException("delete failed"));

        await CreateClient().AddHandler<string, string>(lifetime, _id, Task.FromResult);

        lifetime.Terminate();

        await TestWait.Until(() => _logger.Entries.Count(entry => entry.Level == LogLevel.Warning) == 2);

        _logger.Count(LogLevel.Warning, "Failed to unbind observer").Should().Be(1);
        _logger.Count(LogLevel.Warning, "Failed to delete object reference").Should().Be(1);
    }

    [Fact]
    public void Listener_Cleanup_RunsOnce_RecordFailure_LogsFirstAndEveryTenth()
    {
        var observer = new RuntimePipeObserver(_logger);

        var listener = new RuntimePipeClient.Listener(_id, Guid.NewGuid(), observer,
            Substitute.For<IRuntimePipeObserver>(), _pipe, new SemaphoreSlim(1, 1), Orleans, _logger);

        listener.Cleanup();
        listener.Cleanup();

        _pipe.ReceivedWithAnyArgs(1).UnbindObserver(default);

        for (var i = 0; i < 10; i++)
            listener.RecordFailure(new InvalidOperationException("bind failed"));

        _logger.Count(LogLevel.Error, "(attempt 1) to pipe test-pipe-pipe").Should().Be(1);
        _logger.Count(LogLevel.Error, "(attempt 10) to pipe test-pipe-pipe").Should().Be(1);
        _logger.Entries.Count(entry => entry.Level == LogLevel.Error).Should().Be(2);

        listener.RecordSuccess();
        listener.RecordFailure(new InvalidOperationException("bind failed"));

        _logger.Count(LogLevel.Error, "(attempt 1) to pipe test-pipe-pipe").Should().Be(2);
    }

    [Fact]
    public async Task Loop_BindFails_LogsAndBindsAgainAfterAboutASecond()
    {
        var lifetime = TestLifetime.Child();
        var client = CreateClient();
        var calls = 0;

        await client.AddHandler<string, string>(lifetime, _id, Task.FromResult);

        _pipe.BindObserver(default, default!).ReturnsForAnyArgs(_ => Interlocked.Increment(ref calls) == 1
            ? Task.FromException(new InvalidOperationException("bind failed"))
            : Task.CompletedTask);

        await client.Start(lifetime);

        await TestWait.Until(() => Volatile.Read(ref calls) >= 2);

        _logger.Count(LogLevel.Error, "(attempt 1) to pipe test-pipe-pipe").Should().Be(1);

        lifetime.Terminate();
    }

    [Fact]
    public async Task AddHandler_SecondFailsToBind_TheFirstKeepsThePipe()
    {
        var client = CreateClient();

        await client.AddHandler<string, string>(TestLifetime, _id, Task.FromResult);

        _pipe.BindObserver(default, default!).ThrowsAsyncForAnyArgs(new InvalidOperationException("bind failed"));

        var act = () => client.AddHandler<string, string>(TestLifetime, _id, Task.FromResult);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("bind failed");

        // Only the observer that failed to bind goes: the handler of the first still answers.
        await ((RuntimePipeObserver)_observers[0]).Ping();
    }

    // A pass of the loop has its bind of the old observer on the way when a new handler comes: whatever order the two
    // would reach the grain in, the new one stays bound.
    [Fact]
    public async Task AddHandler_BindOfTheOldOneOnTheWay_TheNewOneStaysBound()
    {
        var pipe = new BindsOfAGrain(_pipe);
        var client = CreateClient();
        var loop = TestLifetime.Child();

        await client.AddHandler<string, string>(TestLifetime, _id, Task.FromResult);
        var old = pipe.Bound;

        pipe.HoldNextBind();
        await client.Start(loop);
        await TestWait.Until(() => pipe.HasHeldBind);

        var adding = client.AddHandler<string, string>(TestLifetime, _id, Task.FromResult);
        pipe.ReleaseHeldBind();
        await adding;

        await TestWait.Until(() => pipe.Unbinds.Contains(old!.Value));

        pipe.Bound.Should().NotBe(old!.Value, "the pipe would stay without a handler until the next refresh, 10-60 s");
        await ((RuntimePipeObserver)_observers[1]).Ping();

        loop.Terminate();
    }

    // A handler ends with its bind on the way: the unbind comes after it, the pipe does not keep the dead observer.
    [Fact]
    public async Task LifetimeEnd_BindOnTheWay_NothingStaysBound()
    {
        var pipe = new BindsOfAGrain(_pipe);
        var client = CreateClient();
        var lifetime = TestLifetime.Child();
        var loop = TestLifetime.Child();

        await client.AddHandler<string, string>(lifetime, _id, Task.FromResult);
        var bound = pipe.Bound;

        pipe.HoldNextBind();
        await client.Start(loop);
        await TestWait.Until(() => pipe.HasHeldBind);

        lifetime.Terminate();
        pipe.ReleaseHeldBind();

        await TestWait.Until(() => pipe.Unbinds.Contains(bound!.Value));

        pipe.Bound.Should().BeNull();

        loop.Terminate();
    }

    // A pass that took the listener before its end does not bring it back.
    [Fact]
    public async Task Listener_ResubscribeAfterCleanup_DoesNotBind()
    {
        var observer = new RuntimePipeObserver(_logger);

        var listener = new RuntimePipeClient.Listener(_id, Guid.NewGuid(), observer,
            Substitute.For<IRuntimePipeObserver>(), _pipe, new SemaphoreSlim(1, 1), Orleans, _logger);

        listener.Cleanup();

        await listener.Resubscribe();

        await _pipe.DidNotReceiveWithAnyArgs().BindObserver(default, default!);
    }

    // The observer of the grain: a bind takes effect when it reaches it, here when the test lets it through.
    private sealed class BindsOfAGrain
    {
        public BindsOfAGrain(IRuntimePipe pipe)
        {
            pipe.BindObserver(default, default!).ReturnsForAnyArgs(call => Bind(call.Arg<Guid>()));

            pipe.UnbindObserver(default).ReturnsForAnyArgs(call => {
                Unbind(call.Arg<Guid>());
                return Task.CompletedTask;
            });
        }

        private readonly Lock _lock = new();
        private readonly TaskCompletionSource _release = new();
        private bool _holdNext;
        private volatile bool _hasHeld;
        private Guid? _bound;
        private readonly List<Guid> _unbinds = new();

        public bool HasHeldBind => _hasHeld;

        public Guid? Bound
        {
            get
            {
                lock (_lock)
                    return _bound;
            }
        }

        public IReadOnlyList<Guid> Unbinds
        {
            get
            {
                lock (_lock)
                    return _unbinds.ToArray();
            }
        }

        public void HoldNextBind()
        {
            lock (_lock)
                _holdNext = true;
        }

        public void ReleaseHeldBind()
        {
            _release.SetResult();
        }

        private async Task Bind(Guid id)
        {
            var isHeld = false;

            lock (_lock)
            {
                isHeld = _holdNext;
                _holdNext = false;
            }

            if (isHeld == true)
            {
                _hasHeld = true;
                await _release.Task;
            }

            lock (_lock)
                _bound = id;
        }

        private void Unbind(Guid id)
        {
            lock (_lock)
            {
                _unbinds.Add(id);

                if (_bound == id)
                    _bound = null;
            }
        }
    }
}
