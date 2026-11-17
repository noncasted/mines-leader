using FluentAssertions;
using Internal;
using Xunit;

namespace Framework.Common.Tests;

// The handler of the reactive exceptions is process-wide (the Unity client installs Debug.LogException): with it a
// failing listener is reported, not thrown, and the pass goes on.
public class ReactiveExceptionsTests : IDisposable
{
    private readonly List<Exception> _reported = new();

    public ReactiveExceptionsTests()
    {
        ReactiveExceptions.SetHandler(_reported.Add);
    }

    public void Dispose()
    {
        ReactiveExceptions.SetHandler(null);
    }

    [Fact]
    public void Lifetime_ListenerThrows_IsReported_OthersStillRun()
    {
        var lifetime = new Lifetime();
        var secondCalled = false;

        lifetime.Listen(() => throw new InvalidOperationException("boom"));
        lifetime.Listen(() => secondCalled = true);

        lifetime.Terminate();

        secondCalled.Should().BeTrue();
        _reported.Should().ContainSingle().Which.Message.Should().Be("boom");
    }

    [Fact]
    public void EventSource_HandlerThrows_IsReported_OthersStillReceive()
    {
        var source = new EventSource<int>();
        var received = new List<int>();

        source.Advise(new Lifetime(), _ => throw new InvalidOperationException("first"));
        source.Advise(new Lifetime(), received.Add);
        source.Advise(new Lifetime(), _ => throw new InvalidOperationException("second"));

        source.Invoke(7);

        received.Should().Equal(7);
        _reported.Select(exception => exception.Message).Should().Equal("first", "second");
    }

    [Fact]
    public void EventSource_HandlerThrows_SubscriptionsAfterwardsStillWork()
    {
        var lifetime = new Lifetime();
        var source = new EventSource();
        var lateCalled = false;

        source.Advise(lifetime, () => throw new InvalidOperationException("boom"));
        source.Invoke();

        source.Advise(lifetime, () => lateCalled = true);
        source.Invoke();

        lateCalled.Should().BeTrue();
        _reported.Should().HaveCount(2);
    }
}
