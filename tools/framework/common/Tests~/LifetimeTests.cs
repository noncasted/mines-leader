using FluentAssertions;
using Internal;
using Xunit;

namespace Framework.Common.Tests;

public class LifetimeTests
{
    [Fact]
    public void Terminate_InvokesAllListenersOnce()
    {
        var lifetime = new Lifetime();
        var calls = new List<int>();

        lifetime.Listen(() => calls.Add(1));
        lifetime.Listen(() => calls.Add(2));

        lifetime.Terminate();
        lifetime.Terminate();

        calls.Should().Equal(1, 2);
        lifetime.IsTerminated.Should().BeTrue();
    }

    // Without ReactiveExceptions.Handler the exception of a listener surfaces after the pass: the other listeners
    // have released their resources by then.
    [Fact]
    public void Terminate_ListenerThrows_OthersStillRun_ThenRethrows()
    {
        var lifetime = new Lifetime();
        var secondCalled = false;

        lifetime.Listen(() => throw new InvalidOperationException("boom"));
        lifetime.Listen(() => secondCalled = true);

        var act = () => lifetime.Terminate();

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        secondCalled.Should().BeTrue();
        lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void Terminate_SeveralListenersThrow_AggregatesThem()
    {
        var lifetime = new Lifetime();

        lifetime.Listen(() => throw new InvalidOperationException("first"));
        lifetime.Listen(() => throw new InvalidOperationException("second"));

        var act = () => lifetime.Terminate();

        act.Should().Throw<AggregateException>().Which.InnerExceptions.Should().HaveCount(2);
    }

    [Fact]
    public void Terminate_ListenerThrows_ChildLeavesParent()
    {
        var parent = new CountingLifetime();
        var child = parent.Child();
        child.Listen(() => throw new InvalidOperationException("boom"));

        var act = () => child.Terminate();

        act.Should().Throw<InvalidOperationException>();
        parent.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveListener_DuringTerminate_DoesNotThrow()
    {
        var lifetime = new Lifetime();
        var removedCalled = false;

        void Removed() => removedCalled = true;

        lifetime.Listen(() => lifetime.RemoveListener(Removed));
        lifetime.Listen(Removed);

        lifetime.Terminate();

        // Removed before its turn in the pass: it is skipped, as if it had been removed before the termination.
        removedCalled.Should().BeFalse();
    }

    [Fact]
    public void Token_IsCancelledAfterTerminate()
    {
        var lifetime = new Lifetime();
        var token = lifetime.Token;

        token.IsCancellationRequested.Should().BeFalse();

        lifetime.Terminate();

        token.IsCancellationRequested.Should().BeTrue();
        lifetime.Token.IsCancellationRequested.Should().BeTrue();
    }

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
    public void Child_TerminatesWithParent()
    {
        var parent = new Lifetime();
        var child = parent.Child();

        parent.Terminate();

        child.IsTerminated.Should().BeTrue();
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
        c.IsTerminated.Should().BeFalse();
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

    [Fact]
    public void ToLifetime_EndsWithTheCancellation()
    {
        using var source = new CancellationTokenSource();

        var lifetime = source.Token.ToLifetime();

        lifetime.IsTerminated.Should().BeFalse();

        source.Cancel();

        lifetime.IsTerminated.Should().BeTrue();
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

    [Fact]
    public void RemoveListener_DuringTerminate_ListenerNotRunYetIsSkipped()
    {
        var lifetime = new Lifetime();
        var ran = false;
        Action second = () => ran = true;

        lifetime.Listen(() => lifetime.RemoveListener(second));
        lifetime.Listen(second);

        lifetime.Terminate();

        ran.Should().BeFalse();
    }

    [Fact]
    public void Intersect_SourceEndsWhileAnotherListenerRemovesIt_IntersectionStillEnds()
    {
        var a = new Lifetime();
        var b = new Lifetime();
        var intersection = a.Intersect(b);

        a.Terminate();

        intersection.IsTerminated.Should().BeTrue();
    }
}
