using Framework.Tests.Fixtures;
using Internal;
using FluentAssertions;
using Game.Session;
using Shared;
using Xunit;

namespace Framework.Tests.Session;

// A session with a reconnect window (SessionCreateOptions.ReconnectWindow): a user whose connection is lost keeps its
// seat (index, entities) for the window and returns to it; a leave or the end of the window ends the seat. Without
// a window a lost connection is a leave. Real sessions on manual time and queues: see TestSession.
public class SessionReconnectTests : LifetimeTest
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    [Fact]
    public void LostConnection_SeatWaits_OthersSeeAway_OwnerPasses()
    {
        var (session, first, second) = CreateOfTwo();

        session.Lose(first);

        session.Lifetime.IsTerminated.Should().BeFalse();
        session.Admission.IsAway(first.BackendId).Should().BeTrue();
        session.Seats.Entries[first.BackendId].Connected.Should().BeNull();
        session.Seats.Entries[first.BackendId].Lifetime.IsTerminated.Should().BeFalse();

        TestSession.Sent<SharedSessionPlayer.RemoteAway>(second).Should().ContainSingle()
                   .Which.Index.Should().Be(first.Index);

        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().BeEmpty();
        session.Owner.Owner.Value.Should().Be(second);
    }

    [Fact]
    public void Return_WithinWindow_SameSeat_IsReturn_EntityCounter_OthersSeeUpdate()
    {
        var (session, first, second) = CreateOfTwo();
        var seat = first.Seat;

        // The client of the first user has created its entity 7 and a session entity 2 in its range.
        CreateEntity(session, first, 7);
        CreateEntity(session, first, 2, new TestSessionPayload());

        session.Lose(first);
        session.Advance(Window - TimeSpan.FromSeconds(1));

        var returned = session.Enter(first.BackendId)!;

        returned.Should().NotBeNull();
        returned.Index.Should().Be(first.Index);
        returned.Id.Should().Be(first.Id);
        returned.Seat.Should().BeSameAs(seat);
        session.Admission.IsAway(first.BackendId).Should().BeFalse();

        var local = TestSession.Sent<SharedSessionPlayer.LocalUpdate>(returned).Should().ContainSingle().Subject;
        local.Index.Should().Be(first.Index);
        local.IsReturn.Should().BeTrue();
        local.EntityCounter.Should().Be(7);
        local.SeatEntities.Should().Be(1, "the entity of the seat, not the session entity it has created");

        TestSession.Sent<SharedSessionPlayer.RemoteUpdate>(second).Select(update => update.Index).Should().Contain(first.Index);

        // The window of the first loss does not end the returned seat.
        session.Advance(TimeSpan.FromSeconds(5));

        seat.Lifetime.IsTerminated.Should().BeFalse();
        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().BeEmpty();
    }

    [Fact]
    public void FirstConnection_IsNotReturn()
    {
        var (_, first, _) = CreateOfTwo();

        var local = TestSession.Sent<SharedSessionPlayer.LocalUpdate>(first).Should().ContainSingle().Subject;
        local.IsReturn.Should().BeFalse();
        local.EntityCounter.Should().Be(0);
        local.SeatEntities.Should().Be(0);
    }

    [Fact]
    public void WindowExpired_SeatEnds_EntitiesDestroyed_OthersSeeDisconnect_ReturnRefused()
    {
        var (session, first, second) = CreateOfTwo();
        var seat = first.Seat;
        var entity = CreateEntity(session, first, 1);

        session.Lose(first);
        entity.Lifetime.IsTerminated.Should().BeFalse("the entities of an away seat wait for its return");

        session.Advance(Window);

        seat.Lifetime.IsTerminated.Should().BeTrue();
        entity.Lifetime.IsTerminated.Should().BeTrue();
        session.Seats.Entries.Should().NotContainKey(first.BackendId);
        session.Admission.IsAway(first.BackendId).Should().BeFalse();

        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().ContainSingle()
                   .Which.Index.Should().Be(first.Index);

        session.Enter(first.BackendId).Should().BeNull();
        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void AllAway_SessionHeld_ClosedWhenTheLastWindowExpires()
    {
        var (session, first, second) = CreateOfTwo();

        session.Lose(first);
        session.Advance(TimeSpan.FromSeconds(10));
        session.Lose(second);

        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(Window - TimeSpan.FromSeconds(10));

        session.Seats.Entries.Should().ContainKey(second.BackendId).And.NotContainKey(first.BackendId);
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(TimeSpan.FromSeconds(10));

        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void AllAway_TheFirstToReturnBecomesOwner_SameIndex()
    {
        var (session, first, second) = CreateOfTwo();

        session.Lose(first);
        session.Lose(second);
        session.Owner.Owner.Value.Should().BeNull();

        var returned = session.Enter(second.BackendId)!;

        returned.Index.Should().Be(second.Index);
        session.Owner.Owner.Value.Should().Be(returned);
    }

    [Fact]
    public void Leave_EndsSeatAtOnce_NoWindow_ReturnRefused()
    {
        var (session, first, second) = CreateOfTwo();
        var entity = CreateEntity(session, first, 1);

        session.Leave(first);

        entity.Lifetime.IsTerminated.Should().BeTrue();
        session.Admission.IsAway(first.BackendId).Should().BeFalse();
        TestSession.Sent<SharedSessionPlayer.RemoteAway>(second).Should().BeEmpty();

        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().ContainSingle()
                   .Which.Index.Should().Be(first.Index);

        session.Enter(first.BackendId).Should().BeNull();
    }

    [Fact]
    public void Leave_ThroughReplacedConnection_Throws()
    {
        var (session, first, _) = CreateOfTwo();
        var returned = session.Enter(first.BackendId)!;

        returned.Seat.Connected.Should().Be(returned);

        var leave = () => first.Seat.MarkLeaving(first);
        leave.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReturnBeforeLossNoticed_StaleDroppedSilently_NoAwayNoDisconnect()
    {
        var (session, first, second) = CreateOfTwo();

        var returned = session.Enter(first.BackendId)!;

        TestSession.Connection(first).ForceDisconnects.Should().Be(1);
        returned.Index.Should().Be(first.Index);
        session.Users.Should().Equal(second, returned);
        session.Admission.IsAway(first.BackendId).Should().BeFalse();
        TestSession.Sent<SharedSessionPlayer.RemoteAway>(second).Should().BeEmpty();
        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().BeEmpty();
        TestSession.Sent<SharedSessionPlayer.LocalUpdate>(returned).Single().IsReturn.Should().BeTrue();

        session.Leave(second);
        session.Leave(returned);

        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    // The old connection is lost between the TryAccept of a return and its join: the join takes the seat back from away,
    // the others see it again, and a later loss is an ordinary one with its own window.
    [Fact]
    public void ReturnAccepted_OldConnectionLostBeforeTheJoin_SeatBack_LaterLossIsAnOrdinaryOne()
    {
        var (session, first, second) = CreateOfTwo();
        var userId = first.BackendId;

        session.Session.TryAccept(userId).Should().BeTrue();
        session.Lose(first);
        session.Admission.IsAway(userId).Should().BeTrue();
        session.ClearSent();

        var returned = session.Join(userId);

        session.Admission.IsAway(userId).Should().BeFalse();
        TestSession.Sent<SharedSessionPlayer.RemoteUpdate>(second).Select(update => update.Index).Should().Contain(first.Index);

        session.Advance(TimeSpan.FromSeconds(10));
        session.Lose(returned);

        session.Admission.IsAway(userId).Should().BeTrue();
        TestSession.Sent<SharedSessionPlayer.RemoteAway>(second).Should().ContainSingle();

        // The window of the first loss ends at 60, the one of the second at 70.
        session.Advance(Window - TimeSpan.FromSeconds(1));
        session.Seats.Entries.Should().ContainKey(userId);

        session.Advance(TimeSpan.FromSeconds(1));
        session.Seats.Entries.Should().NotContainKey(userId);

        session.Leave(second);
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void Return_IgnoresLimitAndSeal()
    {
        var (session, first, _) = CreateOfTwo(maxUsers: 2);

        session.Lose(first);

        // The place of the away seat is held: the session is full.
        session.Session.HasFreePlace(Guid.NewGuid()).Should().BeFalse();
        session.Admission.Seal();

        session.Enter(first.BackendId).Should().NotBeNull();
    }

    [Fact]
    public void ReturningUser_JoinFailed_SeatEnds()
    {
        var (session, first, second) = CreateOfTwo();

        session.Lose(first);
        session.Session.TryAccept(first.BackendId).Should().BeTrue();

        session.Queue.Enqueue(() => session.Session.OnJoinFailed(first.BackendId));
        session.Queue.Drain();

        session.Seats.Entries.Should().NotContainKey(first.BackendId);
        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().ContainSingle();
        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    // A return while the old connection is still joined: the seat has a connection, a failure of the new one ends nothing.
    [Fact]
    public void ReturnWhileStillJoined_JoinFailed_SeatStays()
    {
        var (session, first, second) = CreateOfTwo();

        session.Session.TryAccept(first.BackendId).Should().BeTrue();

        session.Queue.Enqueue(() => session.Session.OnJoinFailed(first.BackendId));
        session.Queue.Drain();

        session.Seats.Entries[first.BackendId].Connected.Should().Be(first);
        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().BeEmpty();
    }

    // A new user that has not made it has no seat: the others are told nothing.
    [Fact]
    public void NewUser_JoinFailed_NoSeat_NothingEnds()
    {
        var (session, _, second) = CreateOfTwo();
        var late = Guid.NewGuid();

        session.Reserve(late).Should().BeTrue();
        session.Session.TryAccept(late).Should().BeTrue();

        session.Session.OnJoinFailed(late);
        session.Queue.Drain();

        session.Seats.Entries.Should().NotContainKey(late);
        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().BeEmpty();
        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    // The session has ended before the failure of a returning user got its turn in the queue.
    [Fact]
    public void ReturningUser_JoinFailed_SessionEndedBeforeTheQueue_NothingIsSent()
    {
        var (session, first, second) = CreateOfTwo();

        session.Lose(first);
        session.Session.TryAccept(first.BackendId).Should().BeTrue();
        session.Session.OnJoinFailed(first.BackendId);

        session.Lifetime.Terminate();
        session.Queue.Drain();

        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().BeEmpty();
    }

    // The timer of the first loss fires while the seat is away again: the window of the second loss is what counts,
    // and the seat ends once though two checks are waiting at its end.
    [Fact]
    public void ReturnedThenLostAgain_TheWindowOfTheSecondLossCounts_SeatEndsOnce()
    {
        var (session, first, second) = CreateOfTwo();
        var userId = first.BackendId;

        session.Lose(first);
        session.Advance(TimeSpan.FromSeconds(40));

        var returned = session.Enter(userId)!;
        session.Advance(TimeSpan.FromSeconds(10));
        session.Lose(returned);

        // The first timer ends at 60, the second window at 50 + 60.
        for (var passed = 50; passed < 109; passed++)
        {
            session.Advance(TimeSpan.FromSeconds(1));
            session.Admission.IsAway(userId).Should().BeTrue();
        }

        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().BeEmpty();
        session.Seats.Entries.Should().ContainKey(userId);

        session.Advance(TimeSpan.FromSeconds(1));

        session.Admission.IsAway(userId).Should().BeFalse();
        session.Seats.Entries.Should().NotContainKey(userId);
        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().ContainSingle();
    }

    // The timer of the first loss belongs to a window that is over: it ends there, nothing polls until the second window.
    [Fact]
    public void ReturnedThenLostAgain_TheOldTimerEnds_NothingPolls()
    {
        var (session, first, _) = CreateOfTwo();
        var userId = first.BackendId;

        session.Lose(first);
        session.Advance(TimeSpan.FromSeconds(40));

        var returned = session.Enter(userId)!;
        session.Advance(TimeSpan.FromSeconds(10));
        session.Lose(returned);

        // The first timer ends at 60.
        session.Advance(TimeSpan.FromSeconds(11));
        var timers = session.Time.CreatedTimers;

        session.Advance(TimeSpan.FromSeconds(1));
        session.Advance(TimeSpan.FromSeconds(1));
        session.Advance(TimeSpan.FromSeconds(1));

        session.Time.CreatedTimers.Should().Be(timers);
        session.Admission.IsAway(userId).Should().BeTrue();
    }

    // The window of an away seat ends while the end of the session is already ahead of the check in the queue.
    [Fact]
    public void WindowExpired_SessionEndedBeforeTheCheck_NothingIsSent()
    {
        var (session, first, second) = CreateOfTwo();

        session.Lose(first);
        session.Lifetime.Terminate();
        session.Time.Advance(Window);
        session.Queue.Drain();

        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().BeEmpty();
    }

    // ReconnectWindow = Zero: no away seats, a lost connection is a leave.
    [Fact]
    public void NoWindow_LostConnection_IsLeave()
    {
        var session = TestSession.Create(reconnectWindow: TimeSpan.Zero);
        var first = session.ReserveAndEnter();
        var second = session.ReserveAndEnter();
        var entity = CreateEntity(session, first, 1);

        session.Lose(first);

        session.Admission.IsAway(first.BackendId).Should().BeFalse();
        session.Seats.Entries.Should().NotContainKey(first.BackendId);
        first.Seat.Lifetime.IsTerminated.Should().BeTrue();
        entity.Lifetime.IsTerminated.Should().BeTrue();

        TestSession.Sent<SharedSessionPlayer.RemoteAway>(second).Should().BeEmpty();
        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().ContainSingle()
                   .Which.Index.Should().Be(first.Index);

        // A connection of the same user needs a reservation (no return to a seat).
        session.Enter(first.BackendId).Should().BeNull();
        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void NoWindow_SecondConnectionWithoutReservation_IsRefused()
    {
        var session = TestSession.Create(reconnectWindow: TimeSpan.Zero);
        var first = session.ReserveAndEnter();

        session.Enter(first.BackendId).Should().BeNull();
        TestSession.Connection(first).ForceDisconnects.Should().Be(0);
    }

    [Fact]
    public void Seat_EntityOutOfItsRange_Throws()
    {
        var seat = new SessionSeat(Guid.NewGuid(), Guid.NewGuid(), 2, new Lifetime(TestLifetime));

        var create = () => seat.OnEntityCreated(SharedEntityIds.Create(3, 1));
        create.Should().Throw<InvalidOperationException>();
    }

    private static (TestSession, IUser, IUser) CreateOfTwo(int maxUsers = 4)
    {
        var session = TestSession.Create(maxUsers: maxUsers, reconnectWindow: Window);
        var first = session.ReserveAndEnter();
        var second = session.ReserveAndEnter();

        return (session, first, second);
    }

    // The client of the user creates an entity with the counter in its range (EntityCreateCommand).
    private static IEntity CreateEntity(TestSession session, IUser creator, int counter, IEntityPayload? payload = null)
    {
        var id = SharedEntityIds.Create(creator.Index, counter);

        session.Request(creator, new SharedSessionEntity.CreateRequest
        {
            Id = id,
            Properties = [],
            Payload = payload ?? new TestPayload()
        });

        return session.Entities.Entries[id];
    }

    // A return while the old connection is still joined: the new connection is not told about the old one (its own
    // seat), the old one hears nothing of the new.
    [Fact]
    public void ReturnWhileStillJoined_NoRemoteUpdateAboutItsOwnSeat()
    {
        var session = TestSession.Create(reconnectWindow: Window);
        var other = session.ReserveAndEnter();
        var userId = Guid.NewGuid();
        var old = session.ReserveAndEnter(userId);
        session.ClearSent();

        var returned = session.Enter(userId)!;

        TestSession.Sent<SharedSessionPlayer.RemoteUpdate>(returned).Should()
                   .ContainSingle().Which.Index.Should().Be(other.Index);

        TestSession.Sent<SharedSessionPlayer.RemoteUpdate>(old).Should().BeEmpty();
    }

    // A refused reservation (a sealed session: a lobby that has started its match) leaves the live connection of the
    // user alone: only a user that gets a place comes anew. A session held only by the user is the other case (its
    // connections are dropped and it closes).
    [Fact]
    public void Reserve_Refused_LiveConnectionStays()
    {
        var session = TestSession.Create();
        var user = session.ReserveAndEnter();
        session.ReserveAndEnter();
        session.Admission.Seal();

        session.Session.Reserve(user.BackendId).Should().NotBe(SessionReservation.Reserved);
        session.Queue.Drain();

        TestSession.Connection(user).ForceDisconnects.Should().Be(0);
        user.Lifetime.IsTerminated.Should().BeFalse();
    }

    // The connections of a seat leave nothing on the session lifetime when they end: returns don't pile listeners up.
    [Fact]
    public void Returns_LeaveNoListenersOnTheSessionLifetime()
    {
        var session = TestSession.Create(reconnectWindow: Window);
        var userId = Guid.NewGuid();
        var user = session.ReserveAndEnter(userId);
        var listeners = ListenerCount(session.Lifetime);

        for (var i = 0; i < 3; i++)
        {
            session.Lose(user);
            user = session.Enter(userId)!;
        }

        ListenerCount(session.Lifetime).Should().Be(listeners);
    }

    // The session owner is a user with a client: a bot never is, the first human is even if a bot came before it, and
    // a leaving owner passes to the next human.
    [Fact]
    public void Owner_IsNeverABot()
    {
        var session = TestSession.Create();
        session.AddBot();

        session.Owner.Owner.Value.Should().BeNull();

        var first = session.ReserveAndEnter();
        session.AddBot();
        var second = session.ReserveAndEnter();

        session.Owner.Owner.Value.Should().Be(first);

        session.Leave(first);

        session.Owner.Owner.Value.Should().Be(second);

        session.Leave(second);

        session.Owner.Owner.Value.Should().BeNull();
    }

    private static int ListenerCount(Lifetime lifetime)
    {
        var field = typeof(Lifetime).GetField("_listeners",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        return ((List<Action>?)field.GetValue(lifetime))?.Count ?? 0;
    }
}
