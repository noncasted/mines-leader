using FluentAssertions;
using Internal;
using Xunit;

namespace Framework.Common.Tests;

public class EventSourceTests
{
    [Fact]
    public void Invoke_DeliversToAllHandlers()
    {
        var lifetime = new Lifetime();
        var source = new EventSource<int>();
        var received = new List<int>();

        source.Advise(lifetime, v => received.Add(v));
        source.Advise(lifetime, v => received.Add(v * 10));

        source.Invoke(3);

        received.Should().Equal(3, 30);
    }

    // Without ReactiveExceptions.Handler the exception surfaces after the pass: every handler has received by then.
    [Fact]
    public void Invoke_HandlerThrows_OthersStillReceive_ThenRethrows()
    {
        var lifetime = new Lifetime();
        var source = new EventSource();
        var secondCalled = false;

        source.Advise(lifetime, () => throw new InvalidOperationException("boom"));
        source.Advise(lifetime, () => secondCalled = true);

        var act = () => source.Invoke();

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        secondCalled.Should().BeTrue();
    }

    [Fact]
    public void LifetimeTerminated_HandlerIsRemoved()
    {
        var lifetime = new Lifetime();
        var source = new EventSource();
        var calls = 0;

        source.Advise(lifetime, () => calls++);

        source.Invoke();
        lifetime.Terminate();
        source.Invoke();

        calls.Should().Be(1);
        source.ListenersCount.Should().Be(0);
    }

    [Fact]
    public void LifetimeTerminated_DuringInvoke_HandlerStillRemovedAfterwards()
    {
        var outer = new Lifetime();
        var inner = new Lifetime();
        var source = new EventSource();
        var innerCalls = 0;

        source.Advise(outer, () => inner.Terminate());
        source.Advise(inner, () => innerCalls++);

        source.Invoke();
        source.Invoke();

        innerCalls.Should().Be(1);
    }

    [Fact]
    public void Advise_DuringInvoke_ReceivesFromNextInvoke()
    {
        var lifetime = new Lifetime();
        var source = new EventSource();
        var lateCalls = 0;
        var subscribed = false;

        source.Advise(lifetime, () => {
            if (subscribed == true)
                return;

            subscribed = true;
            source.Advise(lifetime, () => lateCalls++);
        });

        source.Invoke();
        lateCalls.Should().Be(0);

        source.Invoke();
        lateCalls.Should().Be(1);
    }

    [Fact]
    public void SameHandler_AdvisedTwice_EndOfOneLifetimeRemovesOnlyItsSubscription()
    {
        var first = new Lifetime();
        var second = new Lifetime();
        var source = new EventSource();
        var order = new List<string>();

        void Shared() => order.Add("shared");

        source.Advise(first, Shared);
        source.Advise(first, () => order.Add("other"));
        source.Advise(second, Shared);

        // Removing by the first equal delegate would drop the subscription of "first" and change the order.
        second.Terminate();
        source.Invoke();

        order.Should().Equal("shared", "other");

        order.Clear();
        first.Terminate();
        source.Invoke();

        order.Should().BeEmpty();
    }

    [Fact]
    public void SameHandler_AdvisedTwice_IsInvokedTwice()
    {
        var lifetime = new Lifetime();
        var source = new EventSource<int>();
        var calls = 0;

        void Handler(int _) => calls++;

        source.Advise(lifetime, Handler);
        source.Advise(lifetime, Handler);
        source.Invoke(1);

        calls.Should().Be(2);
    }

    [Fact]
    public void Advise_NullHandler_Throws()
    {
        var source = new EventSource();

        var act = () => source.Advise(new Lifetime(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Dispose_DropsEveryListener()
    {
        var source = new EventSource<int>();
        var calls = 0;

        source.Advise(new Lifetime(), _ => calls++);
        source.Dispose();
        source.Invoke(1);

        calls.Should().Be(0);
        source.ListenersCount.Should().Be(0);
    }
}
