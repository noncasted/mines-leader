using Cluster.Deploy;
using Cluster.Monitoring;
using Cluster.Monitoring.Online;
using FluentAssertions;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster.Online;

// A coordinator replaced by the coordinator of a newer deploy ends its deploy epoch: its tracker must stop, or it
// keeps writing the rows of the days with the frozen online of the old deploy over the ones of the new coordinator.
public class OnlineTrackerEpochTests
{
    [Fact]
    public async Task DeployEpochEnded_ChangesOfTheOnline_WriteNothing()
    {
        var serviceLifetime = new Lifetime();
        var deployLifetime = serviceLifetime.Child();

        var deployContext = Substitute.For<IDeployContext>();
        deployContext.DeployLifetime.Returns(deployLifetime);

        var store = Substitute.For<IOnlineDaysStore>();
        store.Read(Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult<IReadOnlyList<OnlineDayState>>([]));

        var connected = new FakeDeploymentState<ConnectedUsersLiveData>(new ConnectedUsersLiveData());
        var stats = new FakeDeploymentState<OnlineStatsLiveData>(new OnlineStatsLiveData());

        var tracker = new OnlineTracker(connected, stats, store, deployContext, NullLogger<OnlineTracker>.Instance,
            TimeSpan.Zero);

        await tracker.OnCoordinatorSetupCompleted(serviceLifetime);
        await store.Received(1).Write(Arg.Any<IReadOnlyList<OnlineDayState>>());
        store.ClearReceivedCalls();

        deployLifetime.Terminate();

        var data = new ConnectedUsersLiveData();
        data.SetSlice(Guid.NewGuid(), [new ConnectedUserEntry { UserId = Guid.NewGuid() }]);
        connected.Set(data);

        await store.DidNotReceive().Write(Arg.Any<IReadOnlyList<OnlineDayState>>());
        serviceLifetime.Terminate();
    }

    private class FakeDeploymentState<T>(T value) : SharedViewableProperty<T>(value), IDeploymentState<T> where T : class, new()
    {
        public bool IsInitialized => true;

        public Task SetValue(T value)
        {
            Set(value);
            return Task.CompletedTask;
        }
    }
}
