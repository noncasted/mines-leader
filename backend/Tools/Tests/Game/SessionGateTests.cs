using Common.Network;
using FluentAssertions;
using Game.Global;
using Game.Session;
using GameGateway;
using NSubstitute;
using Shared;
using Tests.Security;
using Xunit;

namespace Tests.Game;

// S4: the entry of the game gateway - the user and the session only from a signed ticket, the place only from a
// live reservation of that user (SessionGate in front of ISession.TryAccept).
public class SessionGateTests
{
    [Fact]
    public void ValidTicket_ReservedUser_Enters()
    {
        var world = new Gate();
        var userId = Guid.NewGuid();
        var session = world.AddSession(userId);

        var entry = world.Enter(userId, session.Id);

        entry.Status.Should().Be(SessionAuthStatus.Ok);
        entry.UserId.Should().Be(userId);
        entry.Session.Should().BeSameAs(session);
    }

    // Without a ticket of the meta nobody enters, whatever the client claims.
    [Theory]
    [InlineData("")]
    [InlineData("v1.AAAA.AAAA")]
    public void NoOrMalformedTicket_IsInvalid(string ticket)
    {
        var world = new Gate();
        world.AddSession(Guid.NewGuid());

        world.Sessions.Enter(ticket).Status.Should().Be(SessionAuthStatus.InvalidTicket);
    }

    [Fact]
    public void TicketOfAnotherKey_IsInvalid()
    {
        var world = new Gate();
        var userId = Guid.NewGuid();
        var session = world.AddSession(userId);
        var forged = new SessionTickets(new SessionTicketKey(new byte[32]), world.Time).Issue(userId, session.Id);

        world.Sessions.Enter(forged).Status.Should().Be(SessionAuthStatus.InvalidTicket);
    }

    [Fact]
    public void ExpiredTicket_IsRefused()
    {
        var world = new Gate(joinTimeout: SessionTickets.Lifetime * 2);
        var userId = Guid.NewGuid();
        var session = world.AddSession(userId);
        var ticket = world.Tickets.Issue(userId, session.Id);

        world.Time.Advance(SessionTickets.Lifetime);

        world.Sessions.Enter(ticket).Status.Should().Be(SessionAuthStatus.Expired);
    }

    // The same ticket twice (a copied first frame): the second is refused even with a fresh reservation.
    [Fact]
    public void RepeatedTicket_IsRefused()
    {
        var world = new Gate();
        var userId = Guid.NewGuid();
        var session = world.AddSession(userId);
        var ticket = world.Tickets.Issue(userId, session.Id);

        world.Sessions.Enter(ticket).Status.Should().Be(SessionAuthStatus.Ok);
        session.TryReserve(userId).Should().BeTrue();

        world.Sessions.Enter(ticket).Status.Should().Be(SessionAuthStatus.InvalidTicket);
    }

    // A valid ticket of a user that has no place in the session (another player of the match, an outsider).
    [Fact]
    public void TicketWithoutReservation_NoReservation()
    {
        var world = new Gate();
        var session = world.AddSession(Guid.NewGuid());

        world.Enter(Guid.NewGuid(), session.Id).Status.Should().Be(SessionAuthStatus.NoReservation);
    }

    // A valid ticket of this user into a session it has no place in (its place is in another one).
    [Fact]
    public void TicketForAnotherSession_NoReservation()
    {
        var world = new Gate();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var ownerSession = world.AddSession(owner);
        var otherSession = world.AddSession(other);

        world.Enter(other, ownerSession.Id).Status.Should().Be(SessionAuthStatus.NoReservation);
        world.Enter(other, otherSession.Id).Status.Should().Be(SessionAuthStatus.Ok);
    }

    // A place is used once: a second connection of the same user (a second ticket) needs a new reservation.
    [Fact]
    public void Reservation_IsUsedOnce()
    {
        var world = new Gate();
        var userId = Guid.NewGuid();
        var session = world.AddSession(userId);

        world.Enter(userId, session.Id).Status.Should().Be(SessionAuthStatus.Ok);
        world.Enter(userId, session.Id).Status.Should().Be(SessionAuthStatus.NoReservation);
    }

    [Fact]
    public void ReservationExpired_NoReservation()
    {
        var world = new Gate();
        var userId = Guid.NewGuid();
        var session = world.AddSession(userId);
        var ticket = world.Tickets.Issue(userId, session.Id);

        world.Time.Advance(Gate.JoinTimeout);

        world.Sessions.Enter(ticket).Status.Should().Be(SessionAuthStatus.NoReservation);
    }

    [Fact]
    public void UnknownSession_Closed()
    {
        var world = new Gate();

        world.Enter(Guid.NewGuid(), Guid.NewGuid()).Status.Should().Be(SessionAuthStatus.Closed);
    }

    [Fact]
    public void EndedSession_Closed()
    {
        var world = new Gate();
        var userId = Guid.NewGuid();
        var session = world.AddSession(userId, out var data);

        data.Lifetime.Terminate();

        world.Enter(userId, session.Id).Status.Should().Be(SessionAuthStatus.Closed);
    }

    // The lobby search reserves the place of the user that asked, in an existing lobby or a new one.
    [Fact]
    public void LobbySearch_ReservesThePlaceOfTheUser()
    {
        var world = new Gate();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var lobby = new SessionWorld(SessionType.Lobby, 0, world.Time, Gate.JoinTimeout).Session;
        var factory = Substitute.For<ISessionFactory>();

        factory.CreateLobby(Arg.Any<LobbyCreateOptions>()).Returns(_ => {
            world.Add(lobby);
            return lobby.Id;
        });

        var search = new SessionSearch(factory, world.Collection);

        search.GetOrCreateLobby(first).Should().Be(lobby.Id);
        search.GetOrCreateLobby(second).Should().Be(lobby.Id);
        factory.Received(1).CreateLobby(Arg.Any<LobbyCreateOptions>());

        world.Enter(first, lobby.Id).Status.Should().Be(SessionAuthStatus.Ok);
        world.Enter(second, lobby.Id).Status.Should().Be(SessionAuthStatus.Ok);
        world.Enter(Guid.NewGuid(), lobby.Id).Status.Should().Be(SessionAuthStatus.NoReservation);
    }

    private sealed class Gate
    {
        public static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(30);

        public Gate(TimeSpan? joinTimeout = null)
        {
            _joinTimeout = joinTimeout ?? JoinTimeout;
            Tickets = TestTickets.Create(Time);
            Collection = Substitute.For<ISessionsCollection>();

            Collection.TryGet(Arg.Any<Guid>(), out Arg.Any<ISession>()!).Returns(call => {
                var found = _sessions.TryGetValue(call.ArgAt<Guid>(0), out var session);
                call[1] = session;
                return found;
            });

            Collection.Entries.Returns(_ => _sessions);
            Sessions = new SessionGate(Collection, Tickets);
        }

        private readonly TimeSpan _joinTimeout;
        private readonly Dictionary<Guid, ISession> _sessions = new();

        public ManualTimeProvider Time { get; } = new();
        public SessionTickets Tickets { get; }
        public ISessionsCollection Collection { get; }
        public SessionGate Sessions { get; }

        public ISession AddSession(Guid reservedUser)
        {
            return AddSession(reservedUser, out _);
        }

        public ISession AddSession(Guid reservedUser, out SessionContainerData data)
        {
            var world = new SessionWorld(SessionType.Match, 2, Time, _joinTimeout);
            world.Session.TryReserve(reservedUser).Should().BeTrue();
            Add(world.Session);
            data = world.Data;
            return world.Session;
        }

        public void Add(ISession session)
        {
            _sessions.Add(session.Id, session);
        }

        public SessionGateResult Enter(Guid userId, Guid sessionId)
        {
            return Sessions.Enter(Tickets.Issue(userId, sessionId));
        }
    }
}
