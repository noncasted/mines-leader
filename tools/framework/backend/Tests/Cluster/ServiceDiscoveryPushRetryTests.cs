using Cluster.Deploy;
using Cluster.Discovery;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster;

// A failed push throws to the refresh loop, which logs it and pushes again after its pause (5 s): the loop goes on.
public class ServiceDiscoveryPushRetryTests : LifetimeTest
{
    [Fact]
    public async Task RefreshLoop_PushFails_PushesAgain()
    {
        var member = new ServiceOverview { Id = Guid.NewGuid(), Tag = TestRoles.Meta, UpdateTime = DateTime.UtcNow };
        var updates = 0;

        var storage = Substitute.For<IServiceDiscoveryStorage>();
        storage.Update(default!).ReturnsForAnyArgs(_ => Interlocked.Increment(ref updates) == 1
            ? Task.FromException<Dictionary<Guid, IServiceOverview>>(new InvalidOperationException("storage unavailable"))
            : Task.FromResult(new Dictionary<Guid, IServiceOverview> { [member.Id] = member }));

        var client = Substitute.For<IClusterClient>();
        client.GetGrain<IServiceDiscoveryStorage>(Arg.Any<Guid>(), Arg.Any<string?>()).Returns(storage);

        var orleans = Substitute.For<IOrleans>();
        orleans.Client.Returns(client);

        var deployContext = Substitute.For<IDeployContext>();
        deployContext.DeployId.Returns(Guid.NewGuid());

        var discovery = new ServiceDiscovery(orleans, deployContext, new ServiceEnvironment { Tag = TestRoles.Meta },
            Options.Create(new ServiceDiscoveryOptions()), NullLogger<ServiceDiscovery>.Instance);

        await discovery.Start(TestLifetime);

        await TestWait.Until(() => discovery.Entries.ContainsKey(member.Id));
        Volatile.Read(ref updates).Should().Be(2, "the failed push is followed by the next one of the loop");
    }
}
