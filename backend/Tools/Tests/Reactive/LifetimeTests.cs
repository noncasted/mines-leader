using Common.Reactive;
using FluentAssertions;
using Xunit;

namespace Tests.Reactive;

public class LifetimeTests
{
    [Fact]
    public void Token_RequestedAfterTerminate_IsCancelled()
    {
        var lifetime = new Lifetime();

        lifetime.Terminate();

        lifetime.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Listen_Null_Throws()
    {
        var lifetime = new Lifetime();

        var act = () => lifetime.Listen(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Listen_Terminated_InvokesImmediately()
    {
        var lifetime = new Lifetime();
        lifetime.Terminate();
        var called = false;

        lifetime.Listen(() => called = true);

        called.Should().BeTrue();
    }

    [Fact]
    public void Listen_DuringTerminate_IsInvokedImmediately()
    {
        var lifetime = new Lifetime();
        var lateCalled = false;

        lifetime.Listen(() => lifetime.Listen(() => lateCalled = true));
        lifetime.Terminate();

        lateCalled.Should().BeTrue();
    }

    [Fact]
    public void Child_TerminatedEarly_DetachesFromParent()
    {
        var parent = new Lifetime();
        var child = parent.Child();
        var calls = 0;
        child.Listen(() => calls++);

        child.Terminate();
        parent.Terminate();

        calls.Should().Be(1);
    }

    [Fact]
    public void Intersect_EndsWithEitherSource()
    {
        var a = new Lifetime();
        var b = new Lifetime();

        var first = a.Intersect(b);
        var second = a.Intersect(b);

        a.Terminate();

        first.IsTerminated.Should().BeTrue();
        second.IsTerminated.Should().BeTrue();

        var c = new Lifetime();
        var third = c.Intersect(b);

        b.Terminate();

        third.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void Intersect_TerminatedDirectly_LeavesBothSources()
    {
        var a = new CountingLifetime();
        var b = new CountingLifetime();

        var intersection = a.Intersect(b);

        a.Count.Should().Be(1);
        b.Count.Should().Be(1);

        intersection.Terminate();

        a.Count.Should().Be(0);
        b.Count.Should().Be(0);
    }

    [Fact]
    public void Intersect_WithTerminatedSource_IsTerminatedAndDoesNotSubscribe()
    {
        var a = new Lifetime();
        a.Terminate();
        var b = new CountingLifetime();

        var intersection = a.Intersect(b);

        intersection.IsTerminated.Should().BeTrue();
        b.Count.Should().Be(0);
    }

    // Terminate on one thread races with Listen and Token on others: every listener runs exactly once (either
    // by Terminate or immediately by Listen), and every token handed out ends cancelled.
    [Fact]
    public async Task Terminate_RacingWithListenAndToken_LosesNothing()
    {
        const int rounds = 200;
        const int listenersPerThread = 200;

        for (var round = 0; round < rounds; round++)
        {
            var lifetime = new Lifetime();
            var calls = 0;
            var tokens = new List<CancellationToken>[4];
            using var start = new ManualResetEventSlim(false);

            var workers = Enumerable.Range(0, 4)
                                    .Select(index => Task.Run(() => {
                                        tokens[index] = new List<CancellationToken>();
                                        start.Wait();

                                        for (var i = 0; i < listenersPerThread; i++)
                                        {
                                            lifetime.Listen(() => Interlocked.Increment(ref calls));
                                            tokens[index].Add(lifetime.Token);
                                        }
                                    }))
                                    .ToArray();

            var terminator = Task.Run(() => {
                start.Wait();
                lifetime.Terminate();
            });

            start.Set();
            await Task.WhenAll(workers.Append(terminator));

            calls.Should().Be(4 * listenersPerThread);
            tokens.SelectMany(t => t).Should().OnlyContain(t => t.IsCancellationRequested);
        }
    }

    private sealed class CountingLifetime : IReadOnlyLifetime
    {
        private readonly List<Action> _listeners = new();

        public int Count => _listeners.Count;
        public CancellationToken Token => CancellationToken.None;
        public bool IsTerminated => false;

        public void Listen(Action callback)
        {
            _listeners.Add(callback);
        }

        public void RemoveListener(Action callback)
        {
            _listeners.Remove(callback);
        }
    }
}
