using Cluster.Deploy;
using Common.Reactive;
using Coordinator;
using FluentAssertions;
using Infrastructure;
using Infrastructure.Startup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Tests.Cluster;

public class DeployIdentityTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(20);

    [Fact]
    public async Task HeartbeatFailures_InARow_StopApplication()
    {
        var grain = Substitute.For<IDeployManagement>();
        grain.Heartbeat().Returns(_ => Task.FromException(new InvalidOperationException("storage is down")));

        var host = Substitute.For<IHostApplicationLifetime>();
        var stopped = new TaskCompletionSource();
        host.When(h => h.StopApplication()).Do(_ => stopped.TrySetResult());

        var identity = CreateIdentity(grain, host);
        await identity.StartAsync(TestContext.Current.CancellationToken);

        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await grain.Received(3).Heartbeat();
        await identity.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HeartbeatFailures_InterruptedBySuccess_DoNotStopApplication()
    {
        var calls = 0;
        var enoughCalls = new TaskCompletionSource();
        var grain = Substitute.For<IDeployManagement>();

        // Two failures, one success, repeated: never three failures in a row.
        grain.Heartbeat().Returns(_ => {
            var call = Interlocked.Increment(ref calls);

            if (call >= 12)
                enoughCalls.TrySetResult();

            return call % 3 == 0
                ? Task.CompletedTask
                : Task.FromException(new InvalidOperationException("transient"));
        });

        var host = Substitute.For<IHostApplicationLifetime>();

        var identity = CreateIdentity(grain, host);
        await identity.StartAsync(TestContext.Current.CancellationToken);

        await enoughCalls.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await identity.StopAsync(TestContext.Current.CancellationToken);

        host.DidNotReceive().StopApplication();
    }

    private static DeployIdentity CreateIdentity(IDeployManagement grain, IHostApplicationLifetime host)
    {
        var participantContext = Substitute.For<IClusterParticipantContext>();
        participantContext.IsMessagingStarted.Returns(new ViewableProperty<bool>(true));

        var orleans = Substitute.For<IOrleans>();
        orleans.Client.GetGrain<IDeployManagement>(Arg.Any<Guid>(), Arg.Any<string?>()).Returns(grain);

        var options = Options.Create(new CoordinatorHealthOptions
        {
            HeartbeatInterval = Interval,
            MaxConsecutiveHeartbeatFailures = 3
        });

        return new DeployIdentity(
            participantContext,
            Substitute.For<IDeployContext>(),
            Substitute.For<IMessaging>(),
            orleans,
            host,
            options,
            NullLogger<DeployIdentity>.Instance);
    }
}
