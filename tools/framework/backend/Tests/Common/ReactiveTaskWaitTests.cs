using FluentAssertions;
using Framework.Tests.Fixtures;
using Internal;
using Xunit;

namespace Framework.Tests.Common;

// A completed wait leaves nothing on the long lifetime it was given, and its awaiters do not run inside the Invoke / Set
// that completed it.
public class ReactiveTaskWaitTests : LifetimeTest
{
    [Fact]
    public async Task WaitTrue_Completed_SubscriptionEnds()
    {
        var property = new RecordingProperty();
        var lifetime = new Lifetime(TestLifetime);

        var wait = property.WaitTrue(lifetime);
        property.Push(true);
        await wait;

        property.Subscriptions.Should().ContainSingle().Which.IsTerminated.Should()
                .BeTrue("the subscription of a completed wait must not live on until the lifetime ends");
    }

    [Fact]
    public async Task WaitInvoke_Completed_SubscriptionEnds()
    {
        var source = new RecordingSource();
        var lifetime = new Lifetime(TestLifetime);

        var wait = source.WaitInvoke(lifetime);
        source.Push(5);
        await wait;

        source.Subscriptions.Should().ContainSingle().Which.IsTerminated.Should()
              .BeTrue("the subscription of a completed wait must not live on until the lifetime ends");
    }

    [Fact]
    public async Task WaitTrue_Completed_AwaiterNotInsideSet()
    {
        var property = new ViewableProperty<bool>(false);
        // Per thread on purpose: Set goes on after it completes the wait, so a continuation on a pool thread may run before
        // Set returns and is still not inside it. Only a continuation on the thread in the call of Set sees true.
        using var insideSet = new ThreadLocal<bool>();
        bool? continuationInsideSet = null;

        var wait = property.WaitTrue(new Lifetime(TestLifetime));

        var continuation = wait.ContinueWith(_ => continuationInsideSet = insideSet.Value, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        insideSet.Value = true;
        property.Set(true);
        insideSet.Value = false;

        await continuation;
        continuationInsideSet.Should().BeFalse("the awaiter of a wait must not run inside the Set that completed it");
    }

    private class RecordingProperty : ILifetimedValue<bool>
    {
        private readonly List<(IReadOnlyLifetime Lifetime, Action<IReadOnlyLifetime, bool> Handler)> _handlers = [];

        public List<IReadOnlyLifetime> Subscriptions { get; } = [];
        public bool Value { get; private set; }
        public IReadOnlyLifetime ValueLifetime { get; } = new Lifetime();

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, bool> handler)
        {
            Subscriptions.Add(lifetime);
            _handlers.Add((lifetime, handler));
        }

        public void Push(bool value)
        {
            Value = value;

            foreach (var (lifetime, handler) in _handlers.ToArray())
            {
                if (lifetime.IsTerminated == false)
                    handler(ValueLifetime, value);
            }
        }

        public void Dispose()
        {
        }
    }

    private class RecordingSource : IEventSource<int>
    {
        private readonly List<(IReadOnlyLifetime Lifetime, Action<int> Handler)> _handlers = [];

        public List<IReadOnlyLifetime> Subscriptions { get; } = [];

        public void Advise(IReadOnlyLifetime lifetime, Action<int> handler)
        {
            Subscriptions.Add(lifetime);
            _handlers.Add((lifetime, handler));
        }

        public void Push(int value)
        {
            foreach (var (lifetime, handler) in _handlers.ToArray())
            {
                if (lifetime.IsTerminated == false)
                    handler(value);
            }
        }

        public void Dispose()
        {
        }
    }
}
