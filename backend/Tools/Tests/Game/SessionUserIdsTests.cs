using Common.Reactive;
using FluentAssertions;
using Game.GamePlay;
using Game.Session;
using Meta.Matches;
using Meta.Users;
using NSubstitute;
using Shared;
using Xunit;
using IUser = Game.Session.IUser;

namespace Tests.Game;

// S3: the meta id of a user is its login, the clients of a session know each other only by session ids. The meta
// still gets the meta ids (the match grain, ratings).
public class SessionUserIdsTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Peers_GetSessionIds_NeverTheMetaIds()
    {
        var world = new SessionCloseTests.SessionWorld(SessionType.Match, expectedUsers: 2, joinTimeout: Wait);
        var firstMeta = Guid.NewGuid();
        var secondMeta = Guid.NewGuid();
        world.ConnectUser(firstMeta, out var first);
        world.ConnectUser(secondMeta, out var second);

        var allConnected = world.Session.AllUsersConnected.WaitInvoke(world.Session.Lifetime);
        _ = world.Session.Run();
        await allConnected.WaitAsync(Wait);

        var users = await world.GetUsers();
        var firstUser = users.Single(user => user.BackendId == firstMeta);
        var secondUser = users.Single(user => user.BackendId == secondMeta);

        firstUser.Id.Should().NotBe(firstMeta);
        secondUser.Id.Should().NotBe(secondMeta);

        var firstSent = await ReadPlayerUpdates(first);
        var secondSent = await ReadPlayerUpdates(second);

        firstSent.OfType<SharedSessionPlayer.LocalUpdate>().Single().Id.Should().Be(firstUser.Id);
        firstSent.OfType<SharedSessionPlayer.RemoteUpdate>().Single().Id.Should().Be(secondUser.Id);
        secondSent.OfType<SharedSessionPlayer.LocalUpdate>().Single().Id.Should().Be(secondUser.Id);
        secondSent.OfType<SharedSessionPlayer.RemoteUpdate>().Single().Id.Should().Be(firstUser.Id);

        world.Data.Lifetime.Terminate();
    }

    // A second connection of the same meta user in a session is the same player; another session gives another id.
    [Fact]
    public async Task SessionId_StableInSession_NewInAnother()
    {
        var meta = Guid.NewGuid();
        var world = new SessionCloseTests.SessionWorld(SessionType.Lobby, expectedUsers: 0, joinTimeout: Wait);
        var other = new SessionCloseTests.SessionWorld(SessionType.Lobby, expectedUsers: 0, joinTimeout: Wait);

        world.ConnectUser(meta, out _);
        world.ConnectUser(meta, out _);
        other.ConnectUser(meta, out _);

        await world.WaitUsersCount(2);
        await other.WaitUsersCount(1);

        var ids = (await world.GetUsers()).Select(user => user.Id).Distinct().ToList();
        var otherId = (await other.GetUsers()).Single().Id;

        ids.Should().ContainSingle();
        ids[0].Should().NotBe(meta);
        otherId.Should().NotBe(ids[0]);

        world.Data.Lifetime.Terminate();
        other.Data.Lifetime.Terminate();
    }

    [Fact]
    public void MatchUserIds_MapsBothWays()
    {
        var first = User(Guid.NewGuid(), Guid.NewGuid());
        var second = User(Guid.NewGuid(), Guid.NewGuid());
        var ids = new MatchUserIds([first, second]);

        ids.ToBackend(first.Id).Should().Be(first.BackendId);
        ids.ToBackend([second.Id, first.Id]).Should().Equal(second.BackendId, first.BackendId);

        var stats = ids.ToBackend(new Dictionary<Guid, UserStatsDelta> { [first.Id] = new() });
        stats.Keys.Should().Equal(first.BackendId);

        var summary = ids.ToSession(new MatchCompletionSummary
        {
            Duration = TimeSpan.FromMinutes(3),
            RatingChanges = new Dictionary<Guid, int> { [first.BackendId] = 10, [second.BackendId] = -10 },
            Ratings = new Dictionary<Guid, int> { [first.BackendId] = 1010, [second.BackendId] = 990 }
        });

        summary.Duration.Should().Be(TimeSpan.FromMinutes(3));
        summary.RatingChanges.Should().Equal(new Dictionary<Guid, int> { [first.Id] = 10, [second.Id] = -10 });
        summary.Ratings.Should().Equal(new Dictionary<Guid, int> { [first.Id] = 1010, [second.Id] = 990 });

        var unknown = () => ids.ToBackend(Guid.NewGuid());
        unknown.Should().Throw<KeyNotFoundException>();
    }

    private static IUser User(Guid id, Guid backendId)
    {
        var user = Substitute.For<IUser>();
        user.Id.Returns(id);
        user.BackendId.Returns(backendId);
        return user;
    }

    // The session sends LocalUpdate first, RemoteUpdate when the other user is handled.
    private static async Task<List<INetworkContext>> ReadPlayerUpdates(SessionCloseTests.ScriptedSocket socket)
    {
        var result = new List<INetworkContext>();

        while (result.OfType<SharedSessionPlayer.LocalUpdate>().Any() == false ||
               result.OfType<SharedSessionPlayer.RemoteUpdate>().Any() == false)
            result.Add(await socket.ReadSent(Wait));

        return result;
    }
}
