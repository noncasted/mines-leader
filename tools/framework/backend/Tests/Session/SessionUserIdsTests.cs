using FluentAssertions;
using Game.Session;
using Shared;
using Xunit;

namespace Framework.Tests.Session;

// The meta id of a user (IUser.BackendId) is its login: the clients of a session know each other only by session ids
// (IUser.Id, the id of the seat). The id is new in every session and the same for every seat of the meta user within
// one session.
public class SessionUserIdsTests
{
    [Fact]
    public void Peers_GetSessionIds_NeverTheMetaIds()
    {
        var session = TestSession.Create(expectedUsers: 2);
        var firstMeta = Guid.NewGuid();
        var secondMeta = Guid.NewGuid();
        var first = session.ReserveAndEnter(firstMeta);
        var second = session.ReserveAndEnter(secondMeta);

        first.BackendId.Should().Be(firstMeta);
        second.BackendId.Should().Be(secondMeta);
        first.Id.Should().NotBe(firstMeta);
        second.Id.Should().NotBe(secondMeta);
        first.Id.Should().Be(first.Seat.Id);
        first.Seat.UserId.Should().Be(firstMeta);

        // Both users are handled at once when the second joins: each one may hear about the other more than once.
        TestSession.Sent<SharedSessionPlayer.LocalUpdate>(first).Single().Id.Should().Be(first.Id);
        TestSession.Sent<SharedSessionPlayer.LocalUpdate>(second).Single().Id.Should().Be(second.Id);

        TestSession.Sent<SharedSessionPlayer.RemoteUpdate>(first).Should().NotBeEmpty()
                   .And.OnlyContain(update => update.Index == second.Index && update.Id == second.Id);

        TestSession.Sent<SharedSessionPlayer.RemoteUpdate>(second).Should().NotBeEmpty()
                   .And.OnlyContain(update => update.Index == first.Index && update.Id == first.Id);
    }

    // LocalUpdate carries the session id, and SeatEntities counts only the entities the client of the seat has created
    // (the ones built by the backend come to it as remote ones).
    [Fact]
    public void LocalUpdate_CarriesSessionId_SeatEntitiesExcludeBackendBuilt()
    {
        var session = TestSession.Create(reconnectWindow: TimeSpan.FromSeconds(60));
        var meta = Guid.NewGuid();
        var user = session.ReserveAndEnter(meta);
        session.ReserveAndEnter();

        session.Request(user, new SharedSessionEntity.CreateRequest
        {
            Id = SharedEntityIds.Create(user.Index, 1),
            Properties = [],
            Payload = new TestPayload()
        });

        var built = session.EntityFactory.Create(user.Seat).WithPayload(new TestPayload()).Build();
        built.Owner.Should().BeSameAs(user.Seat);

        session.Lose(user);
        var returned = session.Enter(meta)!;

        var local = TestSession.Sent<SharedSessionPlayer.LocalUpdate>(returned).Should().ContainSingle().Subject;
        local.Id.Should().Be(user.Id);
        local.Id.Should().NotBe(meta);
        local.IsReturn.Should().BeTrue();
        local.SeatEntities.Should().Be(1, "the backend-built entity of the seat is not restored by the client");

        // Both come to the returned client as overviews.
        TestSession.Sent<SharedSessionEntity.CreatedOverview>(returned).Select(overview => overview.EntityId).Should()
                   .BeEquivalentTo([SharedEntityIds.Create(user.Index, 1), built.Id]);
    }

    // A second connection of the same meta user in a session is the same player; another session gives another id.
    [Fact]
    public void SessionId_StableInSession_NewInAnother()
    {
        var meta = Guid.NewGuid();
        var session = TestSession.Create(reconnectWindow: TimeSpan.FromSeconds(60));
        var other = TestSession.Create();

        var first = session.ReserveAndEnter(meta);
        var second = session.Enter(meta)!;
        var elsewhere = other.ReserveAndEnter(meta);

        second.Id.Should().Be(first.Id);
        first.Id.Should().NotBe(meta);
        elsewhere.Id.Should().NotBe(first.Id);
    }

    // The seat of the user has ended (it left); the new seat of the same meta user in this session takes a new index,
    // but keeps the session id.
    [Fact]
    public void NewSeatOfSameMetaUser_KeepsSessionId_TakesNewIndex()
    {
        var session = TestSession.Create();
        var meta = Guid.NewGuid();
        var first = session.ReserveAndEnter(meta);
        var stays = session.ReserveAndEnter();

        session.Leave(first);
        first.Seat.Lifetime.IsTerminated.Should().BeTrue();

        var again = session.ReserveAndEnter(meta);

        again.Seat.Should().NotBeSameAs(first.Seat);
        again.Index.Should().NotBe(first.Index);
        again.Id.Should().Be(first.Id);
        TestSession.Sent<SharedSessionPlayer.LocalUpdate>(again).Single().Id.Should().Be(first.Id);
        TestSession.Sent<SharedSessionPlayer.RemoteUpdate>(stays).Should()
                   .Contain(update => update.Index == again.Index && update.Id == first.Id);
    }

    [Fact]
    public void DifferentMetaUsers_GetDifferentSessionIds()
    {
        var session = TestSession.Create();

        var ids = Enumerable.Range(0, 4).Select(_ => session.ReserveAndEnter().Id).ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Bot_HasSessionId_IsBot()
    {
        var session = TestSession.Create();
        var human = session.ReserveAndEnter();

        var bot = session.AddBot();

        bot.IsBot.Should().BeTrue();
        human.IsBot.Should().BeFalse();
        bot.Id.Should().NotBe(bot.BackendId);
        bot.Index.Should().NotBe(human.Index);
        TestSession.Sent<SharedSessionPlayer.RemoteUpdate>(human).Should()
                   .ContainSingle(update => update.Index == bot.Index && update.Id == bot.Id);
    }
}
