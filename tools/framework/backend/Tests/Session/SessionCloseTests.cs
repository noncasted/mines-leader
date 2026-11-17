using System.Net.WebSockets;
using Common.Network;
using FluentAssertions;
using Framework.Tests.Network;
using Microsoft.Extensions.Logging.Abstractions;
using Game.Session;
using Shared;
using Xunit;

namespace Framework.Tests.Session;

// A session never outlives its users: it closes when the last human leaves (bots don't hold it), when nobody enters
// it, when its expected users don't all join in time. Real sessions on manual time and queues: see TestSession.
public class SessionCloseTests
{
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void LastUserLeft_TerminatesSession()
    {
        var session = TestSession.Create();
        var user = session.ReserveAndEnter();

        session.Leave(user);

        session.Session.IsClosed.Should().BeTrue();
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void LastUserLeft_SessionIsUnavailableBeforeItsLifetimeIsTerminated()
    {
        var session = TestSession.Create();
        var user = session.ReserveAndEnter();
        var connection = TestSession.Connection(user);

        // The user is removed, the termination of the session is still waiting in its queue.
        connection.ReceiveOneWay(new SharedSessionPlayer.Leave());
        connection.Close();

        while (user.Lifetime.IsTerminated == false)
            session.Queue.RunNext().Should().BeTrue();

        session.Session.IsClosed.Should().BeTrue();
        session.Lifetime.IsTerminated.Should().BeFalse();

        var other = Guid.NewGuid();
        session.Reserve(other).Should().BeFalse();
        session.Session.HasFreePlace(other).Should().BeFalse();
        session.Enter(other).Should().BeNull();

        session.Queue.Drain();
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void OwnerLeft_SecondUserStays_OwnerPasses_SessionAlive()
    {
        var session = TestSession.Create();
        var first = session.ReserveAndEnter();
        var second = session.ReserveAndEnter();

        session.Owner.Owner.Value.Should().Be(first);

        session.Leave(first);

        session.Lifetime.IsTerminated.Should().BeFalse();
        session.Owner.Owner.Value.Should().Be(second);
        session.Users.Should().Equal(second);

        session.Leave(second);

        session.Owner.Owner.Value.Should().BeNull();
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void SecondUserLeft_FirstStays_LeftUserReturnsWithReservation()
    {
        var session = TestSession.Create();
        var first = session.ReserveAndEnter();
        var secondId = Guid.NewGuid();
        var second = session.ReserveAndEnter(secondId);

        session.Leave(second);

        session.Lifetime.IsTerminated.Should().BeFalse();
        session.Owner.Owner.Value.Should().Be(first);
        session.Users.Should().Equal(first);

        // No reconnect window: a new place needs a new reservation.
        session.Enter(secondId).Should().BeNull();

        var returned = session.ReserveAndEnter(secondId);

        returned.Index.Should().NotBe(second.Index, "a new seat takes a new index");
        session.Users.Should().HaveCount(2);
    }

    // The client was lost without a close (a domain reload, a crash), its socket is still open. The next reservation
    // of the same user drops that connection: the session left with nobody is closed and refuses the reservation.
    [Fact]
    public void StaleConnectionAlone_ReserveAgain_IsRefused_DropsIt_ClosesSession()
    {
        var session = TestSession.Create();
        var userId = Guid.NewGuid();
        var stale = session.ReserveAndEnter(userId);

        session.Reserve(userId).Should().BeFalse();
        session.Session.IsClosed.Should().BeFalse("the stale connection is dropped from the queue of the session");

        session.Queue.Drain();

        TestSession.Connection(stale).ForceDisconnects.Should().Be(1);
        session.Users.Should().BeEmpty();
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    // Two users: the lost client of the second one comes back while the first plays on - it enters the live session
    // again, its stale connection is dropped and only the first one holds the session meanwhile.
    [Fact]
    public void StaleConnectionBesideLiveUser_ReserveAgain_RejoinsLiveSession()
    {
        var session = TestSession.Create(maxUsers: 2);
        var first = session.ReserveAndEnter();
        var secondId = Guid.NewGuid();
        var stale = session.ReserveAndEnter(secondId);

        // Full with the stale connection, but it is not counted for its own user.
        session.Reserve(secondId).Should().BeTrue();
        session.Queue.Drain();

        TestSession.Connection(stale).ForceDisconnects.Should().Be(1);
        session.Users.Should().Equal(first);
        session.Lifetime.IsTerminated.Should().BeFalse();

        var second = session.Enter(secondId)!;

        session.Users.Should().Equal(first, second);
        session.Owner.Owner.Value.Should().Be(first);
    }

    // A third user is refused by a session of two live users: only the own stale connections are not counted.
    [Fact]
    public void FullSessionOfLiveUsers_ThirdUserIsRefused_NobodyDropped()
    {
        var session = TestSession.Create(maxUsers: 2);
        var first = session.ReserveAndEnter();
        var second = session.ReserveAndEnter();

        session.Reserve(Guid.NewGuid()).Should().BeFalse();
        session.Queue.Drain();

        TestSession.Connection(first).ForceDisconnects.Should().Be(0);
        TestSession.Connection(second).ForceDisconnects.Should().Be(0);
        session.Users.Should().Equal(first, second);
    }

    [Fact]
    public void LiveReservation_HoldsEmptySession_IncomingUserBecomesOwner()
    {
        var session = TestSession.Create();
        var first = session.ReserveAndEnter();
        var secondId = Guid.NewGuid();

        session.Reserve(secondId).Should().BeTrue();
        session.Leave(first);

        session.Lifetime.IsTerminated.Should().BeFalse();
        session.Owner.Owner.Value.Should().BeNull();

        session.Advance(JoinTimeout - TimeSpan.FromSeconds(1));

        var second = session.Enter(secondId);

        second.Should().NotBeNull();
        session.Lifetime.IsTerminated.Should().BeFalse();
        session.Owner.Owner.Value.Should().Be(second);

        // The reservation time passes: the joined user keeps the session.
        session.Advance(JoinTimeout);

        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void ExpiredReservation_DoesNotHoldEmptySession_LateEnterIsRefused()
    {
        var session = TestSession.Create();
        var first = session.ReserveAndEnter();
        var secondId = Guid.NewGuid();

        session.Advance(TimeSpan.FromSeconds(100));

        session.Reserve(secondId).Should().BeTrue();
        session.Leave(first);

        session.Advance(JoinTimeout - TimeSpan.FromSeconds(1));
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(TimeSpan.FromSeconds(1));

        session.Lifetime.IsTerminated.Should().BeTrue();
        session.Enter(secondId).Should().BeNull();
    }

    [Fact]
    public void ReservedButNeverEntered_SessionClosesAfterJoinTimeout()
    {
        var session = TestSession.Create();
        session.Reserve(Guid.NewGuid()).Should().BeTrue();

        session.Advance(JoinTimeout - TimeSpan.FromSeconds(1));
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(TimeSpan.FromSeconds(1));

        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    // Nobody reserves or enters: closed after JoinTimeout of the options.
    [Fact]
    public void NobodyEver_SessionClosesAfterJoinTimeoutOfOptions()
    {
        var timeout = TimeSpan.FromSeconds(5);
        var session = TestSession.Create(joinTimeout: timeout);

        session.Advance(timeout - TimeSpan.FromSeconds(1));
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(TimeSpan.FromSeconds(1));
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void RepeatedReserve_ExtendsReservation()
    {
        var session = TestSession.Create();
        var userId = Guid.NewGuid();
        session.Reserve(userId).Should().BeTrue();

        session.Advance(TimeSpan.FromSeconds(20));
        session.Reserve(userId).Should().BeTrue();

        session.Advance(TimeSpan.FromSeconds(20));
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(TimeSpan.FromSeconds(10));
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void SessionWithUser_IsNotClosedByTime()
    {
        var session = TestSession.Create();
        session.ReserveAndEnter();

        session.Advance(TimeSpan.FromHours(1));

        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void JoinFailed_ClosesEmptySession()
    {
        var session = TestSession.Create();
        var userId = Guid.NewGuid();
        session.Reserve(userId).Should().BeTrue();

        session.Session.TryAccept(userId).Should().BeTrue();
        session.Queue.Enqueue(() => session.Session.OnJoinFailed(userId));
        session.Queue.Drain();

        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void CancelReservation_OfLastHolder_ClosesSession()
    {
        var session = TestSession.Create();
        var userId = Guid.NewGuid();
        session.Reserve(userId).Should().BeTrue();

        session.Session.CancelReservation(userId);
        session.Queue.Drain();

        session.Session.IsClosed.Should().BeTrue();
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void Run_DoesNotTerminateSession()
    {
        var session = TestSession.Create(run: false);
        session.Reserve(Guid.NewGuid()).Should().BeTrue();

        // Run sets the handling up in the session queue.
        session.Run();

        session.RunTask.IsCompletedSuccessfully.Should().BeTrue();
        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    // The expected users have not all joined within JoinTimeout: the session closes (its lifetime ends) and Run
    // completes without handling anybody.
    [Fact]
    public void ExpectedUserNeverJoins_ClosesAfterJoinTimeout()
    {
        var timeout = TimeSpan.FromSeconds(10);
        var session = TestSession.Create(expectedUsers: 2, joinTimeout: timeout);

        var first = session.ReserveAndEnter();

        session.Advance(timeout - TimeSpan.FromSeconds(1));
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(TimeSpan.FromSeconds(1));

        session.Lifetime.IsTerminated.Should().BeTrue();
        session.RunTask.IsCompleted.Should().BeTrue();
        session.Session.AllUsersConnected.IsCanceled.Should().BeTrue();
        TestSession.Sent<SharedSessionPlayer.LocalUpdate>(first).Should().BeEmpty();
    }

    // The session is aborted before it runs the socket of a joined user: the gateway awaiting Closed of the socket returns,
    // and the client gets the close frame.
    [Fact]
    public async Task ExpectedUserNeverJoins_TheSocketOfTheJoinedUserIsClosed()
    {
        var timeout = TimeSpan.FromSeconds(10);
        var session = TestSession.Create(expectedUsers: 2, joinTimeout: timeout);
        var userId = Guid.NewGuid();
        var socket = new ScriptedSocket();
        var connection = new Connection(socket, session.Lifetime, NullLogger.Instance);

        session.Reserve(userId).Should().BeTrue();
        session.Session.TryAccept(userId).Should().BeTrue();
        session.Join(userId, connection);

        session.Advance(timeout);

        session.Lifetime.IsTerminated.Should().BeTrue();
        await connection.Closed.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        socket.SentCloseStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
    }

    [Fact]
    public void ExpectedUsersAllJoined_JoinTimeoutDoesNotClose()
    {
        var session = TestSession.Create(expectedUsers: 2);
        session.ReserveAndEnter();
        session.ReserveAndEnter();

        session.Advance(JoinTimeout * 2);

        session.RunTask.IsCompletedSuccessfully.Should().BeTrue();
        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    // A bot never holds the session: once the last human leaves (a leave or a lost connection), it closes.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HumanWithBot_HumanLeaves_SessionCloses(bool isLeave)
    {
        var session = TestSession.Create(expectedUsers: 2);
        var human = session.ReserveAndEnter();
        var bot = session.AddBot();

        bot.IsBot.Should().BeTrue();
        session.RunTask.IsCompletedSuccessfully.Should().BeTrue("the bot counts for the expected users");
        session.Lifetime.IsTerminated.Should().BeFalse();

        if (isLeave == true)
            session.Leave(human);
        else
            session.Lose(human);

        session.Lifetime.IsTerminated.Should().BeTrue();
        bot.Lifetime.IsTerminated.Should().BeTrue("the bot is connected as long as the session");
    }

    // Bots are not counted by the admission: a session of bots only is empty for it.
    [Fact]
    public void BotsOnly_NoHumanEver_ClosesAfterJoinTimeout()
    {
        var session = TestSession.Create();
        session.AddBot();
        session.AddBot();

        session.Session.HasFreePlace(Guid.NewGuid()).Should().BeTrue();

        session.Advance(JoinTimeout);

        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void BotsTakeNoPlace()
    {
        var session = TestSession.Create(maxUsers: 1);
        session.AddBot();
        session.AddBot();

        session.ReserveAndEnter();

        session.Users.Should().HaveCount(3);
        session.Reserve(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void OneOfTwoHumansLeaves_WithBot_SessionStaysOpen_UntilTheOtherLeaves()
    {
        var session = TestSession.Create();
        var first = session.ReserveAndEnter();
        var second = session.ReserveAndEnter();
        session.AddBot();

        session.Lose(first);
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Leave(second);
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    // The reservation and the gateway on their own thread against the session queue that removes the last user.
    // Either the user enters a live session, or it is refused; nothing throws.
    [Fact]
    public void EnterConcurrentlyWithLastUserLeaving_EntersLiveSessionOrIsRefused()
    {
        for (var i = 0; i < 300; i++)
        {
            var session = TestSession.Create();
            var first = session.ReserveAndEnter();
            var secondId = Guid.NewGuid();

            var reserved = false;
            var accepted = false;

            using var barrier = new Barrier(2);

            Parallel.Invoke(
                () => {
                    barrier.SignalAndWait();
                    session.Leave(first);
                },
                () => {
                    barrier.SignalAndWait();
                    reserved = session.Reserve(secondId);
                    accepted = session.Session.TryAccept(secondId);
                });

            accepted.Should().Be(reserved);

            if (reserved == true)
            {
                session.Lifetime.IsTerminated.Should().BeFalse("the reservation holds the session");

                var second = session.Join(secondId);
                session.Owner.Owner.Value.Should().Be(second);
            }
            else
            {
                session.Queue.Drain();
                session.Lifetime.IsTerminated.Should().BeTrue();
            }
        }
    }

    // A connection dropped before the session handles its users (nobody runs it yet, Common.Network.Connection.Run
    // would refuse it) is removed at once: the session left with nobody is closed.
    [Fact]
    public void ConnectionDroppedBeforeStart_UserIsRemoved_SessionCloses()
    {
        var session = TestSession.Create(expectedUsers: 2);
        var user = session.ReserveAndEnter();

        session.Session.DropConnections(user.BackendId);
        session.Queue.Drain();

        user.Lifetime.IsTerminated.Should().BeTrue();
        session.Users.Should().BeEmpty();
        session.Session.IsClosed.Should().BeTrue();
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    // Coming anew drops the connections of that user, not the bots of the session.
    [Fact]
    public void DropConnections_BotStays()
    {
        var session = TestSession.Create();
        var bot = session.AddBot();
        var user = session.ReserveAndEnter();
        var other = session.ReserveAndEnter();

        session.Session.DropConnections(user.BackendId);
        session.Queue.Drain();

        user.Lifetime.IsTerminated.Should().BeTrue();
        bot.Lifetime.IsTerminated.Should().BeFalse();
        other.Lifetime.IsTerminated.Should().BeFalse();
    }

    // Whatever the run of a connection ends with, its user is removed: a failed run is a disconnect.
    [Fact]
    public void ConnectionRunFails_UserIsRemoved_SessionCloses()
    {
        var session = TestSession.Create();
        var userId = Guid.NewGuid();
        session.Reserve(userId).Should().BeTrue();
        session.Session.TryAccept(userId).Should().BeTrue();

        var user = session.Join(userId, new TestConnection(session.Session.Lifetime)
        {
            RunFailure = new IOException("The socket has failed")
        });

        user.Lifetime.IsTerminated.Should().BeTrue();
        session.Users.Should().BeEmpty();
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    // The expected users have not all joined in time: the session is closed (no reservation, no entry) before its
    // lifetime ends, like every other close - also while a reservation made later is still alive (no user leaves to
    // close it on the way).
    [Fact]
    public void ExpectedUserNeverJoins_ClosedBeforeTerminated()
    {
        var session = TestSession.Create(expectedUsers: 2, joinTimeout: JoinTimeout);
        session.Advance(TimeSpan.FromSeconds(10));
        session.Reserve(Guid.NewGuid()).Should().BeTrue();

        bool? closedAtTheEnd = null;
        session.Lifetime.Listen(() => closedAtTheEnd = session.Session.IsClosed);

        session.Advance(JoinTimeout - TimeSpan.FromSeconds(10));

        session.Lifetime.IsTerminated.Should().BeTrue();
        closedAtTheEnd.Should().BeTrue();
        session.Session.Reserve(Guid.NewGuid()).Should().NotBe(SessionReservation.Reserved);
    }

    // Nothing of a session that has ended waits on the clock: its timers queue no action after its end (the join
    // timeout, the empty checks of the reservations, the reconnect window of an away seat).
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void SessionEnded_ItsTimersQueueNothing(int expectedUsers)
    {
        var session = TestSession.Create(expectedUsers: expectedUsers, reconnectWindow: TimeSpan.FromSeconds(60));
        var user = session.ReserveAndEnter();
        session.Reserve(Guid.NewGuid()).Should().BeTrue();

        if (expectedUsers == 0)
            session.Lose(user);

        session.Lifetime.Terminate();
        session.Queue.Drain();

        session.Time.Advance(TimeSpan.FromMinutes(5));

        session.Queue.Count.Should().Be(0);
    }
}
