using Cluster.Deploy;
using Cluster.Monitoring;
using Framework.Tests.Fixtures;
using Internal;
using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster;

public class DeployContextTests : LifetimeTest
{
    [Fact]
    public async Task Set_ManyEpochs_ParentKeepsOnlyCurrentEpochListener()
    {
        var parent = new CountingLifetime();
        var context = new DeployContext([], NullLogger<DeployContext>.Instance);

        for (var i = 0; i < 5; i++)
            await context.Set(Guid.NewGuid(), parent);

        parent.ActiveListeners.Should().Be(1);

        parent.Terminate();
        context.DeployLifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public async Task Set_SubscriberThrows_PropagatesAndEndsEpoch()
    {
        var failing = Substitute.For<IDeployAware>();
        failing.OnDeployChanged(Arg.Any<Guid>(), Arg.Any<IReadOnlyLifetime>())
               .Returns(Task.FromException(new InvalidOperationException("load failed")));

        var context = new DeployContext([failing], NullLogger<DeployContext>.Instance);
        var deployId = Guid.NewGuid();

        var act = () => context.Set(deployId, new Lifetime(TestLifetime));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("load failed");
        context.DeployLifetime.IsTerminated.Should().BeTrue();

        // The same id is attached again on the next call instead of being treated as already set.
        failing.ClearReceivedCalls();
        await act.Should().ThrowAsync<InvalidOperationException>();
        await failing.Received(1).OnDeployChanged(deployId, Arg.Any<IReadOnlyLifetime>());
    }

    // A second switch that comes while a slow subscriber holds the first one must not let the first one go on
    // afterwards and hand its old deploy to the subscribers after the slow one.
    [Fact]
    public async Task Set_SecondDuringSlowFirst_SubscribersEndOnTheSecondDeploy()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var slowFirst = new TaskCompletionSource();

        var slow = Substitute.For<IDeployAware>();
        slow.OnDeployChanged(first, Arg.Any<IReadOnlyLifetime>()).Returns(slowFirst.Task);
        slow.OnDeployChanged(second, Arg.Any<IReadOnlyLifetime>()).Returns(Task.CompletedTask);

        var seen = new List<(Guid DeployId, bool IsTerminated)>();
        var next = Substitute.For<IDeployAware>();
        next.OnDeployChanged(Arg.Any<Guid>(), Arg.Any<IReadOnlyLifetime>()).Returns(call => {
            seen.Add((call.ArgAt<Guid>(0), call.ArgAt<IReadOnlyLifetime>(1).IsTerminated));
            return Task.CompletedTask;
        });

        var context = new DeployContext([slow, next], NullLogger<DeployContext>.Instance);
        var parent = new Lifetime(TestLifetime);

        var firstSet = context.Set(first, parent);
        var secondSet = context.Set(second, parent);

        slowFirst.SetResult();
        await Task.WhenAll(firstSet, secondSet);

        context.DeployId.Should().Be(second);
        context.DeployLifetime.IsTerminated.Should().BeFalse();
        seen.Should().NotContain(entry => entry.IsTerminated, "a subscriber must not get an epoch that has already ended");
        seen[^1].DeployId.Should().Be(second);
    }

    [Fact]
    public async Task Set_LeftDuringSlowSubscriber_StopsBeforeTheNextOne()
    {
        var deployId = Guid.NewGuid();
        var slowSet = new TaskCompletionSource();

        var slow = Substitute.For<IDeployAware>();
        slow.OnDeployChanged(deployId, Arg.Any<IReadOnlyLifetime>()).Returns(slowSet.Task);

        var next = Substitute.For<IDeployAware>();
        var context = new DeployContext([slow, next], NullLogger<DeployContext>.Instance);

        var set = context.Set(deployId, new Lifetime(TestLifetime));
        context.Leave();
        slowSet.SetResult();
        await set;

        await next.DidNotReceive().OnDeployChanged(Arg.Any<Guid>(), Arg.Any<IReadOnlyLifetime>());
    }

    [Fact]
    public async Task DeploymentState_LoadFails_ThrowsAndIsNotInitialized()
    {
        var orleans = Substitute.For<IOrleans>();
        var reads = 0;

        orleans.StateStorage.Read<AddressableStateValue>(Arg.Any<StateIdentity>()).Returns(_ => {
            if (Interlocked.Increment(ref reads) == 1)
                return Task.FromResult(new AddressableStateValue());

            return Task.FromException<AddressableStateValue>(new InvalidOperationException("db down"));
        });

        var state = new DeploymentState<LiveSessionsData>(
            orleans,
            Substitute.For<IMessaging>(),
            NullLoggerFactory.Instance,
            new LiveSessionsData());

        await state.OnDeployChanged(Guid.NewGuid(), new Lifetime(TestLifetime));
        state.IsInitialized.Should().BeTrue();

        var act = () => state.OnDeployChanged(Guid.NewGuid(), new Lifetime(TestLifetime));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("db down");
        state.IsInitialized.Should().BeFalse("the value of the previous epoch must not look valid for the new one");
    }

    private class CountingLifetime : IReadOnlyLifetime
    {
        private readonly Lifetime _inner = new();

        public int ActiveListeners { get; private set; }

        public CancellationToken Token => _inner.Token;
        public bool IsTerminated => _inner.IsTerminated;

        public void Listen(Action callback)
        {
            ActiveListeners++;
            _inner.Listen(callback);
        }

        public void RemoveListener(Action callback)
        {
            ActiveListeners--;
            _inner.RemoveListener(callback);
        }

        public void Terminate()
        {
            _inner.Terminate();
        }
    }
}
