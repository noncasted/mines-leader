using Cluster.Coordination;
using Cluster.Deploy;
using Cluster.Discovery;
using Common.Extensions;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Infrastructure.Execution;
using Infrastructure.Startup;
using Internal;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster;

// The wait of a participant for the cluster does not flood the log. The startup replaces the process-wide handler of
// NoAwait, so the test runs in the collection of the tests that replace it.
[Collection(nameof(NoAwaitCollection))]
public class ClusterParticipantStartupLogTests
{
    [Fact]
    public async Task WaitClusterReady_MissingService_WarnsOnce()
    {
        // The startup installs its handler of the unobserved tasks when it finds none: static, the test puts it back.
        var previousHandler = NoAwaitExceptions.Handler;

        var logger = new RecordingLogger<ClusterParticipantStartup>();

        var loopObserver = Substitute.For<IServiceLoopObserver>();
        loopObserver.IsOrleansStarted.Returns(new ViewableProperty<bool>(true));

        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimePipe.Exists(DeployIdPipe.Id).Returns(true);
        messaging.RuntimePipe.Send<DeployIdResponse>(DeployIdPipe.Id, Arg.Any<object>())
                 .Returns(new DeployIdResponse { DeployId = Guid.NewGuid() });

        var discovery = Substitute.For<IServiceDiscovery>();
        discovery.Self.Returns(new ServiceOverview { Id = Guid.NewGuid(), Tag = TestRoles.Meta, UpdateTime = DateTime.UtcNow });
        discovery.Entries.Returns(new Dictionary<Guid, IServiceOverview>());

        var startupConfig = Substitute.For<IClusterStartupConfig>();
        startupConfig.RequiredServices.Returns([TestRoles.Silo]);

        var startup = new ClusterParticipantStartup(
            Substitute.For<ITaskBalancer>(),
            discovery,
            loopObserver,
            Substitute.For<IServiceLoop>(),
            messaging,
            Substitute.For<IOrleans>(),
            Substitute.For<IDeployContext>(),
            Substitute.For<IClusterParticipantContext>(),
            startupConfig,
            Substitute.For<IHostApplicationLifetime>(),
            logger);

        try
        {
            await startup.StartAsync(TestContext.Current.CancellationToken);

            // Polls of the same missing set: the first logs, the next ones must not (the 25th logs again, 5 s later).
            await TestWait.Until(() => discovery.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(discovery.Push)) >= 4);
            await startup.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            NoAwaitExceptions.SetHandler(previousHandler);
        }

        logger.Count("cluster not ready").Should().BeLessThanOrEqualTo(1,
            "the missing services are logged when they change, not on every poll");
    }
}
