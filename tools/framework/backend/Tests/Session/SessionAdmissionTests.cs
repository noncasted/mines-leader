using FluentAssertions;
using Game.Session;
using Shared;
using Xunit;

namespace Framework.Tests.Session;

// The admission rule alone: synchronous, the time is passed by the caller, the timeouts come from the options.
public class SessionAdmissionTests
{
    private const int MaxUsers = 2;

    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReconnectWindow = TimeSpan.FromSeconds(60);

    [Fact]
    public void LastUserLeft_ClosesAndRefusesReserveAndAccept()
    {
        var admission = Create();
        var user = Join(admission, Start);

        var closed = admission.OnUserLeft(user, Start + Tick);

        closed.Should().BeTrue();
        admission.IsClosed.Should().BeTrue();
        admission.Reserve(Guid.NewGuid(), MaxUsers, Start + Tick).Should().NotBe(SessionReservation.Reserved);
        admission.Reserve(user, MaxUsers, Start + Tick).Should().NotBe(SessionReservation.Reserved);
        admission.TryAccept(user, Start + Tick, false).Should().BeFalse();
    }

    [Fact]
    public void NotLastUserLeft_StaysOpen()
    {
        var admission = Create();
        var user = Join(admission, Start);
        Join(admission, Start);

        var closed = admission.OnUserLeft(user, Start + Tick);

        closed.Should().BeFalse();
        admission.IsClosed.Should().BeFalse();
        admission.Reserve(Guid.NewGuid(), MaxUsers, Start + Tick).Should().Be(SessionReservation.Reserved);
    }

    [Fact]
    public void ReservationBeforeLastUserLeft_HoldsSession_UntilItExpires()
    {
        var admission = Create();
        var user = Join(admission, Start);
        var incoming = Guid.NewGuid();
        admission.Reserve(incoming, MaxUsers, Start + Tick).Should().Be(SessionReservation.Reserved);

        admission.OnUserLeft(user, Start + Tick * 2).Should().BeFalse();

        var beforeExpiry = Start + Tick + JoinTimeout - TimeSpan.FromMilliseconds(1);
        admission.TryCloseIfEmpty(beforeExpiry).Should().BeFalse();
        admission.IsClosed.Should().BeFalse();

        var atExpiry = Start + Tick + JoinTimeout;
        admission.TryCloseIfEmpty(atExpiry).Should().BeTrue();
        admission.IsClosed.Should().BeTrue();
        admission.TryAccept(incoming, atExpiry, false).Should().BeFalse();
    }

    // The reservation lives for SessionCreateOptions.JoinTimeout, not for a constant.
    [Fact]
    public void Reservation_LivesForJoinTimeoutOfOptions()
    {
        var timeout = TimeSpan.FromSeconds(5);
        var admission = Create(joinTimeout: timeout);
        admission.Reserve(Guid.NewGuid(), MaxUsers, Start).Should().Be(SessionReservation.Reserved);

        admission.GetReservationsTimeout(Start).Should().Be(timeout);
        admission.TryCloseIfEmpty(Start + timeout - Tick).Should().BeFalse();
        admission.TryCloseIfEmpty(Start + timeout).Should().BeTrue();
    }

    [Fact]
    public void ReservationBeforeLastUserLeft_IncomingEntersLiveSession()
    {
        var admission = Create();
        var user = Join(admission, Start);
        var incoming = Guid.NewGuid();
        admission.Reserve(incoming, MaxUsers, Start).Should().Be(SessionReservation.Reserved);

        admission.OnUserLeft(user, Start + Tick).Should().BeFalse();
        admission.TryAccept(incoming, Start + Tick, false).Should().BeTrue();
        admission.OnUserJoined(incoming);

        // The reservation time has passed, but the user is in the session.
        admission.TryCloseIfEmpty(Start + JoinTimeout * 2).Should().BeFalse();
        admission.OnUserLeft(incoming, Start + JoinTimeout * 2).Should().BeTrue();
    }

    [Fact]
    public void ReservationAfterLastUserLeft_IsRefused()
    {
        var admission = Create();
        var user = Join(admission, Start);

        admission.OnUserLeft(user, Start + Tick).Should().BeTrue();

        admission.Reserve(Guid.NewGuid(), MaxUsers, Start + Tick).Should().NotBe(SessionReservation.Reserved);
    }

    [Fact]
    public void AcceptedUser_HoldsSession_WhileJoining()
    {
        var admission = Create();
        var user = Join(admission, Start);
        var incoming = Guid.NewGuid();
        admission.Reserve(incoming, MaxUsers, Start).Should().Be(SessionReservation.Reserved);
        admission.TryAccept(incoming, Start, false).Should().BeTrue();

        // The last joined user leaves and the reservation time passes while the incoming user is between the accept
        // and the creation in the session queue.
        admission.OnUserLeft(user, Start + JoinTimeout * 2).Should().BeFalse();
        admission.IsClosed.Should().BeFalse();

        admission.OnUserJoined(incoming);
        admission.OnUserLeft(incoming, Start + JoinTimeout * 3).Should().BeTrue();
    }

    [Fact]
    public void JoinFailed_ReleasesPlaceAndClosesEmptySession()
    {
        var admission = Create();
        var incoming = Guid.NewGuid();
        admission.Reserve(incoming, MaxUsers, Start).Should().Be(SessionReservation.Reserved);
        admission.TryAccept(incoming, Start, false).Should().BeTrue();

        admission.OnJoinFailed(incoming, Start + Tick).Should().BeTrue();

        admission.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void JoinFailed_OfUserNotJoining_Throws()
    {
        var admission = Create();

        var failed = () => admission.OnJoinFailed(Guid.NewGuid(), Start);

        failed.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NeverEnteredSession_ClosesOnlyAfterReservationExpires()
    {
        var admission = Create();
        admission.Reserve(Guid.NewGuid(), MaxUsers, Start).Should().Be(SessionReservation.Reserved);

        admission.TryCloseIfEmpty(Start + Tick).Should().BeFalse();
        admission.TryCloseIfEmpty(Start + JoinTimeout).Should().BeTrue();
    }

    [Fact]
    public void SessionWithoutReservations_ClosesOnCheck()
    {
        var admission = Create();

        admission.TryCloseIfEmpty(Start).Should().BeTrue();
        admission.TryCloseIfEmpty(Start).Should().BeFalse("the session is closed only once");
    }

    [Fact]
    public void TwoReservations_ThirdUserIsRefused()
    {
        var admission = Create();

        admission.Reserve(Guid.NewGuid(), MaxUsers, Start).Should().Be(SessionReservation.Reserved);
        admission.Reserve(Guid.NewGuid(), MaxUsers, Start).Should().Be(SessionReservation.Reserved);
        admission.Reserve(Guid.NewGuid(), MaxUsers, Start).Should().NotBe(SessionReservation.Reserved);
    }

    [Fact]
    public void JoinedUserAndReservation_ThirdUserIsRefused()
    {
        var admission = Create();
        Join(admission, Start);

        admission.Reserve(Guid.NewGuid(), MaxUsers, Start).Should().Be(SessionReservation.Reserved);
        admission.Reserve(Guid.NewGuid(), MaxUsers, Start).Should().NotBe(SessionReservation.Reserved);
    }

    [Fact]
    public void RepeatedReservationOfSameUser_IsNotDoubled_AndIsRefreshed()
    {
        var admission = Create();
        var user = Guid.NewGuid();

        admission.Reserve(user, MaxUsers, Start).Should().Be(SessionReservation.Reserved);
        admission.Reserve(user, MaxUsers, Start + Tick * 10).Should().Be(SessionReservation.Reserved);

        admission.Reserve(Guid.NewGuid(), MaxUsers, Start + Tick * 10).Should().Be(SessionReservation.Reserved, "the first user holds one place");
        admission.GetReservationsTimeout(Start + Tick * 10).Should().Be(JoinTimeout);
    }

    [Fact]
    public void ExpiredReservation_FreesPlace()
    {
        var admission = Create();
        Join(admission, Start);
        admission.Reserve(Guid.NewGuid(), MaxUsers, Start).Should().Be(SessionReservation.Reserved);

        admission.Reserve(Guid.NewGuid(), MaxUsers, Start + Tick).Should().NotBe(SessionReservation.Reserved);
        admission.Reserve(Guid.NewGuid(), MaxUsers, Start + JoinTimeout).Should().Be(SessionReservation.Reserved);
    }

    // The place comes only from a live reservation of the user: the limit and the seal are checked there.
    [Fact]
    public void AcceptWithoutReservation_IsRefused()
    {
        var admission = Create();
        var user = Join(admission, Start);

        admission.TryAccept(Guid.NewGuid(), Start, false).Should().BeFalse();

        admission.IsClosed.Should().BeFalse();
        admission.OnUserLeft(user, Start + Tick).Should().BeTrue();
    }

    [Fact]
    public void AcceptAfterReservationExpired_IsRefused()
    {
        var admission = Create();
        var user = Join(admission, Start);
        var incoming = Guid.NewGuid();
        admission.Reserve(incoming, MaxUsers, Start).Should().Be(SessionReservation.Reserved);

        admission.TryAccept(incoming, Start + JoinTimeout, false).Should().BeFalse();

        admission.OnUserLeft(user, Start + JoinTimeout).Should().BeTrue();
    }

    // One reservation - one connection: a second entry with the same place is refused.
    [Fact]
    public void AcceptTwiceWithOneReservation_SecondIsRefused()
    {
        var admission = Create();
        var incoming = Guid.NewGuid();
        admission.Reserve(incoming, MaxUsers, Start).Should().Be(SessionReservation.Reserved);

        admission.TryAccept(incoming, Start, false).Should().BeTrue();
        admission.TryAccept(incoming, Start, false).Should().BeFalse();
    }

    // The seal stops new reservations; a place reserved before it is still entered.
    [Fact]
    public void Sealed_ReservedUserEnters_OthersCannotReserve()
    {
        var admission = Create();
        var reserved = Guid.NewGuid();
        admission.Reserve(reserved, MaxUsers, Start).Should().Be(SessionReservation.Reserved);

        admission.Seal();

        admission.IsSealed.Should().BeTrue();
        admission.HasFreePlace(Guid.NewGuid(), MaxUsers, Start).Should().BeFalse();
        admission.Reserve(Guid.NewGuid(), MaxUsers, Start).Should().NotBe(SessionReservation.Reserved);
        admission.TryAccept(Guid.NewGuid(), Start, false).Should().BeFalse();
        admission.TryAccept(reserved, Start, false).Should().BeTrue();
    }

    // MaxUsers holds through the reservation: a user over the limit gets no reservation and so no entry.
    [Fact]
    public void OverLimit_NoReservation_NoEntry()
    {
        var admission = Create();
        Join(admission, Start);
        Join(admission, Start);
        var extra = Guid.NewGuid();

        admission.Reserve(extra, MaxUsers, Start).Should().NotBe(SessionReservation.Reserved);
        admission.TryAccept(extra, Start, false).Should().BeFalse();
    }

    // A user that reserves again has lost its client: its joined connection is stale.
    [Fact]
    public void StaleConnectionAlone_ReserveOfSameUserIsRefused_SessionClosesWhenItIsDropped()
    {
        var admission = Create();
        var user = Join(admission, Start);

        admission.IsJoined(user).Should().BeTrue();
        admission.HasFreePlace(user, MaxUsers, Start + Tick).Should().BeFalse();
        admission.Reserve(user, MaxUsers, Start + Tick).Should().NotBe(SessionReservation.Reserved);

        admission.OnUserLeft(user, Start + Tick).Should().BeTrue();
        admission.IsJoined(user).Should().BeFalse();
    }

    [Fact]
    public void StaleConnectionBesideLiveUser_TakesNoPlaceForItsOwnUser()
    {
        var admission = Create();
        var live = Join(admission, Start);
        var user = Join(admission, Start);

        admission.HasFreePlace(Guid.NewGuid(), MaxUsers, Start).Should().BeFalse("two joined users fill the session");
        admission.HasFreePlace(user, MaxUsers, Start).Should().BeTrue();
        admission.Reserve(user, MaxUsers, Start).Should().Be(SessionReservation.Reserved);

        // The stale connection leaves, the reservation and the live user hold the session.
        admission.OnUserLeft(user, Start + Tick).Should().BeFalse();
        admission.TryAccept(user, Start + Tick, false).Should().BeTrue();
        admission.OnUserJoined(user);

        admission.OnUserLeft(live, Start + Tick).Should().BeFalse();
        admission.OnUserLeft(user, Start + Tick).Should().BeTrue();
    }

    [Fact]
    public void SameUserJoinedTwice_LeavesTwice()
    {
        var admission = Create();
        var other = Join(admission, Start);
        var user = Join(admission, Start);

        // The second connection comes with its own reservation (its first connection is stale for it).
        admission.Reserve(user, MaxUsers, Start + Tick).Should().Be(SessionReservation.Reserved);
        admission.TryAccept(user, Start + Tick, false).Should().BeTrue();
        admission.OnUserJoined(user);

        admission.OnUserLeft(user, Start + Tick).Should().BeFalse();
        admission.IsJoined(user).Should().BeTrue();
        admission.OnUserLeft(user, Start + Tick).Should().BeFalse();
        admission.IsJoined(user).Should().BeFalse();
        admission.OnUserLeft(other, Start + Tick).Should().BeTrue();

        var leaveAgain = () => admission.OnUserLeft(user, Start + Tick);
        leaveAgain.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetReservationsTimeout_ReturnsTimeToLastExpiry()
    {
        var admission = Create();
        admission.GetReservationsTimeout(Start).Should().BeNull();

        admission.Reserve(Guid.NewGuid(), MaxUsers, Start).Should().Be(SessionReservation.Reserved);
        admission.Reserve(Guid.NewGuid(), MaxUsers, Start + Tick * 5).Should().Be(SessionReservation.Reserved);

        admission.GetReservationsTimeout(Start + Tick * 10).Should().Be(JoinTimeout - Tick * 5);
        admission.GetReservationsTimeout(Start + Tick * 5 + JoinTimeout).Should().BeNull();
    }

    [Fact]
    public void GetPendingUsers_ReservedAndJoining_NotJoined()
    {
        var admission = Create();
        var joined = Join(admission, Start);
        var reserved = Guid.NewGuid();
        var joining = Guid.NewGuid();
        admission.Reserve(reserved, int.MaxValue, Start).Should().Be(SessionReservation.Reserved);
        admission.Reserve(joining, int.MaxValue, Start).Should().Be(SessionReservation.Reserved);
        admission.TryAccept(joining, Start, false).Should().BeTrue();

        admission.GetPendingUsers(Start).Should().BeEquivalentTo([reserved, joining]);
        admission.GetPendingUsers(Start + JoinTimeout).Should().Equal(joining);
        admission.GetPendingUsers(Start).Should().NotContain(joined);
    }

    [Fact]
    public void CancelReservation_LastHolder_ClosesSession()
    {
        var admission = Create();
        var user = Guid.NewGuid();
        admission.Reserve(user, MaxUsers, Start).Should().Be(SessionReservation.Reserved);

        admission.CancelReservation(Guid.NewGuid(), Start).Should().BeFalse("no reservation, nothing changes");
        admission.CancelReservation(user, Start).Should().BeTrue();
        admission.IsClosed.Should().BeTrue();
    }

    // A lost connection in a session with a reconnect window: only a return accepts, the away seat holds the session
    // until the session expires it.
    [Fact]
    public void AwaySeat_OnlyReturnAccepts_HoldsUntilExpiry()
    {
        var admission = Create(reconnectWindow: ReconnectWindow);
        var user = Guid.NewGuid();

        admission.Reserve(user, 4, Start).Should().Be(SessionReservation.Reserved);
        admission.TryAccept(user, Start, true).Should().BeTrue();
        admission.OnUserJoined(user);
        admission.OnUserAway(user, Start);
        admission.OnUserLeft(user, Start).Should().BeFalse("the away seat holds the session");

        admission.IsAway(user).Should().BeTrue();
        admission.TryAccept(user, Start, false).Should().BeFalse("no return without allowsReturn");
        admission.TryExpireAway(user, Start + ReconnectWindow - Tick).Should().BeFalse();
        admission.TryCloseIfEmpty(Start + ReconnectWindow * 2).Should().BeFalse("only the session expires a seat");

        admission.TryExpireAway(user, Start + ReconnectWindow).Should().BeTrue();
        admission.IsAway(user).Should().BeFalse();
        admission.TryAccept(user, Start + ReconnectWindow, true).Should().BeFalse();
        admission.TryCloseIfEmpty(Start + ReconnectWindow).Should().BeTrue();
    }

    [Fact]
    public void AwaySeat_TakesPlaceOfItsUser()
    {
        var admission = Create(reconnectWindow: ReconnectWindow);
        var away = Join(admission, Start);
        Join(admission, Start);
        admission.OnUserAway(away, Start);
        admission.OnUserLeft(away, Start).Should().BeFalse();

        admission.HasFreePlace(Guid.NewGuid(), MaxUsers, Start).Should().BeFalse();
        admission.TryAccept(away, Start, true).Should().BeTrue("the place is its own: no limit");
    }

    [Fact]
    public void Away_OfNotJoinedUser_Throws()
    {
        var admission = Create(reconnectWindow: ReconnectWindow);

        var away = () => admission.OnUserAway(Guid.NewGuid(), Start);
        away.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Away_WithoutReconnectWindow_Throws()
    {
        var admission = Create();
        var user = Join(admission, Start);

        var away = () => admission.OnUserAway(user, Start);
        away.Should().Throw<InvalidOperationException>().WithMessage("*without a reconnect window*");
    }

    // The entering user (reserve, then the gateway) against the last user leaving, on real threads. Whatever the
    // interleaving: the entering user either gets into a live session or gets an explicit refusal - never an accepted
    // place in a closed session.
    [Fact]
    public void EnterConcurrentlyWithLastUserLeaving_EntersLiveSessionOrIsRefused()
    {
        var entered = 0;
        var refused = 0;

        for (var i = 0; i < 2000; i++)
        {
            var admission = Create();
            var user = Join(admission, Start);
            var incoming = Guid.NewGuid();
            var reserved = false;
            var accepted = false;
            var closedByLeave = false;

            using var barrier = new Barrier(2);

            Parallel.Invoke(
                () => {
                    barrier.SignalAndWait();
                    closedByLeave = admission.OnUserLeft(user, Start + Tick);
                },
                () => {
                    barrier.SignalAndWait();
                    reserved = admission.Reserve(incoming, MaxUsers, Start + Tick) == SessionReservation.Reserved;
                    accepted = admission.TryAccept(incoming, Start + Tick, false);
                });

            accepted.Should().Be(reserved);
            closedByLeave.Should().Be(reserved == false);
            admission.IsClosed.Should().Be(accepted == false);

            if (accepted == true)
            {
                admission.OnUserJoined(incoming);
                admission.OnUserLeft(incoming, Start + Tick * 2).Should().BeTrue();
                entered++;
            }
            else
            {
                refused++;
            }
        }

        (entered + refused).Should().Be(2000);
    }

    // The gateway accept alone (no reservation) against the last user leaving: always refused, the leave closes the
    // session whatever the interleaving.
    [Fact]
    public void AcceptConcurrentlyWithLastUserLeaving_IsRefused()
    {
        for (var i = 0; i < 2000; i++)
        {
            var admission = Create();
            var user = Join(admission, Start);
            var incoming = Guid.NewGuid();
            var accepted = false;
            var closedByLeave = false;

            using var barrier = new Barrier(2);

            Parallel.Invoke(
                () => {
                    barrier.SignalAndWait();
                    closedByLeave = admission.OnUserLeft(user, Start + Tick);
                },
                () => {
                    barrier.SignalAndWait();
                    accepted = admission.TryAccept(incoming, Start + Tick, false);
                });

            accepted.Should().BeFalse();
            closedByLeave.Should().BeTrue();
            admission.IsClosed.Should().BeTrue();
        }
    }

    [Fact]
    public void OnUserJoined_ClosedSession_Throws()
    {
        var admission = Create();
        admission.Close();

        var join = () => admission.OnUserJoined(Guid.NewGuid());

        join.Should().Throw<InvalidOperationException>();
    }

    // A user that joins without TryAccept consumes its own reservation: only the connection holds the session then.
    [Fact]
    public void OnUserJoined_WithoutAccept_ConsumesTheReservation()
    {
        var admission = Create();
        var user = Guid.NewGuid();

        admission.Reserve(user, MaxUsers, Start).Should().Be(SessionReservation.Reserved);
        admission.OnUserJoined(user);

        admission.GetPendingUsers(Start).Should().BeEmpty();
        admission.GetReservationsTimeout(Start).Should().BeNull();
        admission.OnUserLeft(user, Start).Should().BeTrue();
    }

    [Fact]
    public void OnUserAway_ClosedSession_Throws()
    {
        var admission = Create(reconnectWindow: ReconnectWindow);
        var user = Join(admission, Start);
        admission.Close();

        var away = () => admission.OnUserAway(user, Start);

        away.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OnUserAway_AlreadyAway_Throws()
    {
        var admission = Create(reconnectWindow: ReconnectWindow);
        var user = Join(admission, Start);
        admission.OnUserAway(user, Start);

        var away = () => admission.OnUserAway(user, Start);

        away.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryExpireAway_UserIsNotAway_False()
    {
        var admission = Create(reconnectWindow: ReconnectWindow);
        var user = Join(admission, Start);

        admission.TryExpireAway(user, Start + ReconnectWindow).Should().BeFalse();
    }

    // Two returns on their way at once: each one holds the session until it joins or fails.
    [Fact]
    public void TwoReturnsOfOneUser_EachHoldsTheSession_TheLastFailureCloses()
    {
        var admission = Create(reconnectWindow: ReconnectWindow);
        var user = Join(admission, Start);

        admission.TryAccept(user, Start, true).Should().BeTrue();
        admission.TryAccept(user, Start, true).Should().BeTrue();
        admission.OnUserLeft(user, Start).Should().BeFalse();

        admission.GetPendingUsers(Start).Should().ContainSingle().Which.Should().Be(user);

        admission.OnJoinFailed(user, Start).Should().BeFalse();
        admission.GetPendingUsers(Start).Should().ContainSingle().Which.Should().Be(user);

        admission.OnJoinFailed(user, Start).Should().BeTrue();
        admission.IsClosed.Should().BeTrue();
    }

    // The own away seat of a user that reserves anew is not somebody else holding the session.
    [Fact]
    public void AwayUserWithStaleConnection_OwnAwaySeatDoesNotCount_HeldOnlyByIt()
    {
        var admission = Create(reconnectWindow: ReconnectWindow);
        var user = Join(admission, Start);
        admission.OnUserAway(user, Start);

        admission.Reserve(user, MaxUsers, Start).Should().Be(SessionReservation.HeldOnlyByOwnStale);
        admission.HasFreePlace(user, MaxUsers, Start).Should().BeFalse();
    }

    // The away seat of another user holds the session and takes a place.
    [Fact]
    public void AwaySeatOfAnotherUser_HoldsTheSession_TakesAPlace()
    {
        var admission = Create(reconnectWindow: ReconnectWindow);
        var user = Join(admission, Start);
        var other = Join(admission, Start);
        admission.OnUserAway(other, Start);
        admission.OnUserLeft(other, Start);

        admission.HasFreePlace(Guid.NewGuid(), 2, Start).Should().BeFalse();
        admission.HasFreePlace(Guid.NewGuid(), 3, Start).Should().BeTrue();
        admission.Reserve(user, 2, Start).Should().Be(SessionReservation.Reserved);
    }

    private static SessionAdmission Create(TimeSpan? joinTimeout = null, TimeSpan? reconnectWindow = null)
    {
        return new SessionAdmission(new SessionCreateOptions
        {
            Type = SessionType.Match,
            ExpectedUsers = 0,
            MaxUsers = MaxUsers,
            IsPrivate = false,
            JoinTimeout = joinTimeout ?? JoinTimeout,
            ReconnectWindow = reconnectWindow ?? TimeSpan.Zero
        });
    }

    private static Guid Join(SessionAdmission admission, DateTime now)
    {
        var user = Guid.NewGuid();

        admission.Reserve(user, int.MaxValue, now).Should().Be(SessionReservation.Reserved);
        admission.TryAccept(user, now, false).Should().BeTrue();
        admission.OnUserJoined(user);

        return user;
    }
}
