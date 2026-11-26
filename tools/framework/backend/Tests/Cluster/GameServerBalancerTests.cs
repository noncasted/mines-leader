using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Monitoring;
using FluentAssertions;
using Framework.Tests.Fixtures;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.Cluster;

// F2.15: a new session goes to the game server with the fewest live sessions (LiveSessionsData slices).
public class GameServerBalancerTests
{
    [Fact]
    public void Pick_ServerWithFewestSessions()
    {
        var busy = Server();
        var free = Server();
        var balancer = Create(Discovery(busy, free), Slices((busy, 3), (free, 1)));

        for (var i = 0; i < 20; i++)
            balancer.Pick().Should().BeSameAs(free);
    }

    [Fact]
    public void Pick_ServerWithoutSlice_HasNoSessions()
    {
        var busy = Server();
        var fresh = Server();
        var balancer = Create(Discovery(busy, fresh), Slices((busy, 1)));

        balancer.Pick().Should().BeSameAs(fresh);
    }

    [Fact]
    public void Pick_Tie_SpreadsAmongFewest()
    {
        var a = Server();
        var b = Server();
        var busy = Server();
        var balancer = Create(Discovery(a, b, busy), Slices((a, 2), (b, 2), (busy, 5)));

        var picked = Enumerable.Range(0, 200).Select(_ => balancer.Pick()).ToHashSet();

        picked.Should().BeEquivalentTo(new[] { a, b });
    }

    [Fact]
    public void Pick_IgnoresServicesThatAreNotGameServers()
    {
        var game = Server();

        var meta = new ServiceOverview
        {
            Id = Guid.NewGuid(),
            Tag = TestRoles.Meta,
            UpdateTime = DateTime.UtcNow
        };

        var discovery = Substitute.For<IServiceDiscovery>();

        discovery.Entries.Returns(new Dictionary<Guid, IServiceOverview>
        {
            [meta.Id] = meta,
            [game.Id] = game
        });

        Create(discovery).Pick().Should().BeSameAs(game);
    }

    [Fact]
    public void Pick_NoGameServers_Throws()
    {
        var pick = () => Create(Discovery()).Pick();

        pick.Should().Throw<InvalidOperationException>();
    }

    // A balancer over the discovery with no live sessions yet (other tests of the session requester).
    public static IGameServerBalancer Create(IServiceDiscovery discovery, LiveSessionsData? data = null)
    {
        var liveSessions = Substitute.For<IDeploymentState<LiveSessionsData>>();
        liveSessions.Value.Returns(data ?? new LiveSessionsData());

        return new GameServerBalancer(discovery, liveSessions);
    }

    private static GameServerOverview Server()
    {
        return new GameServerOverview
        {
            Id = Guid.NewGuid(),
            Tag = TestRoles.Game,
            UpdateTime = DateTime.UtcNow,
            Url = "http://localhost:7102"
        };
    }

    private static IServiceDiscovery Discovery(params GameServerOverview[] servers)
    {
        var discovery = Substitute.For<IServiceDiscovery>();
        discovery.Entries.Returns(servers.ToDictionary(server => server.Id, server => (IServiceOverview)server));
        return discovery;
    }

    private static LiveSessionsData Slices(params (GameServerOverview Server, int Sessions)[] slices)
    {
        var data = new LiveSessionsData();

        foreach (var (server, sessions) in slices)
        {
            data.SetSlice(server.Id, Enumerable.Range(0, sessions)
                                               .Select(_ => new LiveSessionEntry { Id = Guid.NewGuid() })
                                               .ToList());
        }

        return data;
    }
}
