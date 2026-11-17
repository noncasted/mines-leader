using Cluster.Lobbies;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster;

// A reload of the previous deploy still in flight after the switch must not put its snapshot over the lobbies of the
// new deploy: the registry of the new deploy counts its versions from 0 again.
public class OpenLobbiesWatcherEpochTests : LifetimeTest
{
    [Fact]
    public async Task ReloadOfThePreviousDeploy_CompletesAfterTheSwitch_IsDropped()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var lateReload = new TaskCompletionSource<OpenLobbies>();

        var firstRegistry = Substitute.For<ILobbyRegistry>();

        firstRegistry.GetOpenLobbies()
                     .Returns(Task.FromResult(new OpenLobbies { Version = 1 }), lateReload.Task);

        var secondRegistry = Substitute.For<ILobbyRegistry>();
        var secondLobby = new OpenLobby { Code = "NEW" };

        secondRegistry.GetOpenLobbies()
                      .Returns(Task.FromResult(new OpenLobbies { Version = 0, Entries = [secondLobby] }));

        var orleans = Substitute.For<IOrleans>();
        orleans.Grains.GetGrain<ILobbyRegistry>(first).Returns(firstRegistry);
        orleans.Grains.GetGrain<ILobbyRegistry>(second).Returns(secondRegistry);

        var channel = new InMemoryChannelClient();
        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimeChannel.Returns(channel);

        var watcher = new OpenLobbiesWatcher(orleans, messaging, NullLogger<OpenLobbiesWatcher>.Instance);
        var firstLifetime = new Lifetime(TestLifetime);
        await watcher.OnDeployChanged(first, firstLifetime);

        channel.ReportGap();

        firstLifetime.Terminate();
        await watcher.OnDeployChanged(second, new Lifetime(TestLifetime));

        lateReload.SetResult(new OpenLobbies { Version = 7, Entries = [new OpenLobby { Code = "OLD" }] });

        watcher.Lobbies.Value.Entries.Should().Equal(secondLobby);
    }
}
