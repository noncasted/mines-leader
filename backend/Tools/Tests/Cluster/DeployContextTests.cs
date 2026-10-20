using Cluster.Deploy;
using Cluster.Monitoring;
using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Tests.Cluster;

public class DeployContextTests
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

        var act = () => context.Set(deployId, new Lifetime());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("load failed");
        context.DeployLifetime.IsTerminated.Should().BeTrue();

        // The same id is attached again on the next call instead of being treated as already set.
        failing.ClearReceivedCalls();
        await act.Should().ThrowAsync<InvalidOperationException>();
        await failing.Received(1).OnDeployChanged(deployId, Arg.Any<IReadOnlyLifetime>());
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

        var state = new DeploymentState<LiveMatchesData>(
            orleans,
            Substitute.For<IMessaging>(),
            NullLoggerFactory.Instance,
            new LiveMatchesData());

        await state.OnDeployChanged(Guid.NewGuid(), new Lifetime());
        state.IsInitialized.Should().BeTrue();

        var act = () => state.OnDeployChanged(Guid.NewGuid(), new Lifetime());

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
