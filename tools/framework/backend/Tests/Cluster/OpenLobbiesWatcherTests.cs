using Cluster.Lobbies;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster;

// OpenLobbiesWatcher over a substituted registry and an in-memory channel: a gap of the channel (missed pushes)
// reloads the open lobbies from the registry.
public class OpenLobbiesWatcherTests : LifetimeTest
{
    [Fact]
    public async Task GapDetected_ReloadsFromTheRegistry()
    {
        var deployId = Guid.NewGuid();
        var registry = Substitute.For<ILobbyRegistry>();

        registry.GetOpenLobbies()
                .Returns(Task.FromResult(new OpenLobbies { Version = 1 }),
                    Task.FromResult(new OpenLobbies { Version = 3 }));

        var orleans = Substitute.For<IOrleans>();
        orleans.Grains.GetGrain<ILobbyRegistry>(deployId).Returns(registry);

        var channel = new InMemoryChannelClient();
        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimeChannel.Returns(channel);

        var watcher = new OpenLobbiesWatcher(orleans, messaging, NullLogger<OpenLobbiesWatcher>.Instance);
        await watcher.OnDeployChanged(deployId, new Lifetime(TestLifetime));

        watcher.Lobbies.Value.Version.Should().Be(1);

        channel.ReportGap();

        await TestWait.Until(() => watcher.Lobbies.Value.Version == 3);
    }
}
