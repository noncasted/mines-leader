using Cluster.Discovery;
using Common.Network;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Cluster;
using Framework.Tests.Security;
using Framework.Tests.Session;
using Game.Hosting;
using Game.Lobbies;
using Game.Session;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.Hosting;

// The meta side of the session requests against the real handlers of one game server (SessionEndpoints over
// HostingTestWorld): the pipe between them is a direct call, the result (the response of the meta command) is
// returned and carries the ticket of the user.
public class SessionRequesterTests : LifetimeTest
{
    [Fact]
    public async Task CreateLobby_SendsOkWithCodeSessionAndServerUrl()
    {
        var meta = new MetaWorld(TestLifetime);

        var result = await meta.Create(Guid.NewGuid());

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);
        LobbyCode.IsValid(result.Code).Should().BeTrue();
        result.ServerUrl.Should().Be(meta.Game.WebSocketUrl);
        meta.Game.Sessions.Should().ContainKey(result.SessionId);
        meta.Game.Sessions[result.SessionId].LobbyInfo().Code.Should().Be(result.Code);
    }

    [Fact]
    public async Task JoinLobby_ByCodeAsTyped_SendsOkWithTheSameSession()
    {
        var meta = new MetaWorld(TestLifetime);
        var guest = Guid.NewGuid();
        var created = await meta.Create(Guid.NewGuid());

        var result = await meta.Join(guest, $"  {created.Code.ToLowerInvariant()} ");

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);
        result.SessionId.Should().Be(created.SessionId);
        result.ServerUrl.Should().Be(created.ServerUrl);
        result.Code.Should().Be(created.Code);
        meta.Game.Enter(created.SessionId, guest).Should().NotBeNull();
    }

    [Fact]
    public async Task JoinLobby_UnknownCode_SendsNotFound()
    {
        var meta = new MetaWorld(TestLifetime);
        var created = await meta.Create(Guid.NewGuid());
        var unknown = created.Code == "ZZZZZZ" ? "YYYYYY" : "ZZZZZZ";

        var result = await meta.Join(Guid.NewGuid(), unknown);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.NotFound);
        result.SessionId.Should().Be(Guid.Empty);
        result.ServerUrl.Should().BeEmpty();
        meta.Pipe.Count<SessionPayloads.LobbyJoin.Request>().Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC")]
    [InlineData("ABCDEFG")]
    [InlineData("ABCD10")]
    [InlineData("ABC-EF")]
    public async Task JoinLobby_NotACode_SendsNotFound(string code)
    {
        var meta = new MetaWorld(TestLifetime);
        await meta.Create(Guid.NewGuid());

        var result = await meta.Join(Guid.NewGuid(), code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.NotFound);
        meta.Pipe.Count<SessionPayloads.LobbyJoin.Request>().Should().Be(0);
    }

    [Fact]
    public async Task JoinLobby_FullLobby_SendsFull()
    {
        var meta = new MetaWorld(TestLifetime);
        var created = await meta.Create(Guid.NewGuid());

        for (var i = 0; i < 3; i++)
            (await meta.Join(Guid.NewGuid(), created.Code)).Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);

        var result = await meta.Join(Guid.NewGuid(), created.Code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.Full);
        result.SessionId.Should().Be(Guid.Empty);
        result.ServerUrl.Should().BeEmpty();
    }

    [Fact]
    public async Task JoinLobby_ClosedLobby_SendsNotFound()
    {
        var meta = new MetaWorld(TestLifetime);
        var creator = Guid.NewGuid();
        var created = await meta.Create(creator);

        var user = meta.Game.Enter(created.SessionId, creator)!;
        meta.Game.Disconnect(meta.Game.Sessions[created.SessionId], user);

        var result = await meta.Join(Guid.NewGuid(), created.Code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.NotFound);
    }

    [Fact]
    public async Task JoinLobby_ServerOfTheLobbyIsGone_SendsNotFound()
    {
        var meta = new MetaWorld(TestLifetime);
        var created = await meta.Create(Guid.NewGuid());

        meta.Game.RemoveServers();

        var result = await meta.Join(Guid.NewGuid(), created.Code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.NotFound);
        meta.Pipe.Count<SessionPayloads.LobbyJoin.Request>().Should().Be(0);
    }

    // The code of a lobby from the start of its countdown to the end of its match.

    [Fact]
    public async Task JoinLobby_DuringCountdown_SendsOk()
    {
        var meta = new MetaWorld(TestLifetime);
        var lobby = await TestLobby.Create(meta.Game, guests: 0);
        var guest = Guid.NewGuid();

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(3));

        var result = await meta.Join(guest, lobby.State.Code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);
        result.SessionId.Should().Be(lobby.Session.Session.Id);
        meta.Game.Enter(result.SessionId, guest).Should().NotBeNull();
    }

    [Fact]
    public async Task JoinLobby_CancelledCountdown_SendsOk()
    {
        var meta = new MetaWorld(TestLifetime);
        var lobby = await TestLobby.Create(meta.Game, guests: 0);

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(3));
        lobby.WriteStart(false);
        lobby.Advance(TimeSpan.FromSeconds(10));

        var result = await meta.Join(Guid.NewGuid(), lobby.State.Code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);
        result.SessionId.Should().Be(lobby.Session.Session.Id);
    }

    [Fact]
    public async Task JoinLobby_OwnCodeFromLiveLobby_SendsOkWithTheSameSession()
    {
        var meta = new MetaWorld(TestLifetime);
        var lobby = await TestLobby.Create(meta.Game, guests: 1);

        var result = await meta.Join(lobby.Users[1].BackendId, lobby.State.Code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);
        result.SessionId.Should().Be(lobby.Session.Session.Id);
    }

    [Fact]
    public async Task JoinLobby_RightAfterZero_LobbyIsNotClosedYet_SendsTheLobbyOfLatePlayers()
    {
        var meta = new MetaWorld(TestLifetime);
        var lobby = await TestLobby.Create(meta.Game, guests: 1);
        var code = lobby.State.Code;

        lobby.RunCountdown();

        lobby.Session.Lifetime.IsTerminated.Should().BeFalse();

        var result = await meta.Join(Guid.NewGuid(), code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);
        result.SessionId.Should().NotBe(lobby.Session.Session.Id);
        result.SessionId.Should().NotBe(lobby.Game.SessionId);
        result.Code.Should().Be(code);

        var running = meta.Game.Sessions[result.SessionId];
        running.LobbyInfo().RunningMatchId.Should().Be(lobby.Game.SessionId);
        running.LobbyInfo().Code.Should().Be(code);
    }

    [Fact]
    public async Task JoinLobby_RightAfterZero_RegistryIsNotToldYet_GameServerSendsStarted()
    {
        var meta = new MetaWorld(TestLifetime);
        var lobby = await TestLobby.Create(meta.Game, guests: 1);

        meta.Game.Codes.HoldMarkStarted(new TaskCompletionSource());
        lobby.RunCountdown();

        var result = await meta.Join(Guid.NewGuid(), lobby.State.Code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.Started);
        result.SessionId.Should().Be(Guid.Empty);
        meta.Pipe.Count<SessionPayloads.LobbyJoin.Request>().Should().Be(1);
    }

    [Fact]
    public async Task JoinLobby_MatchIsGoing_LobbyIsClosed_SendsTheLobbyOfLatePlayers_NoPlaceInTheMatchIsReserved()
    {
        var meta = new MetaWorld(TestLifetime);
        var lobby = await TestLobby.Create(meta.Game, guests: 1);
        var code = lobby.State.Code;
        var stranger = Guid.NewGuid();

        lobby.RunCountdown();
        var match = meta.Game.Sessions[lobby.Game.SessionId];

        foreach (var user in lobby.Users)
        {
            meta.Game.Enter(match.Session.Id, user.BackendId).Should().NotBeNull();
            meta.Game.Disconnect(lobby.Session, user);
        }

        lobby.Session.Lifetime.IsTerminated.Should().BeTrue();

        var result = await meta.Join(stranger, code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);
        result.ServerUrl.Should().Be(meta.Game.WebSocketUrl);
        meta.Game.Sessions[result.SessionId].LobbyInfo().RunningMatchId.Should().Be(match.Session.Id);

        // the lobby of the late players holds no place in the match: the entry into the match takes it
        match.Admission.GetPendingUsers(meta.Game.Time.GetUtcNow().UtcDateTime).Should().BeEmpty();
        match.Users.Count.Should().Be(2);

        var running = meta.Game.Enter(result.SessionId, stranger);
        running.Should().NotBeNull();

        var second = await meta.Join(Guid.NewGuid(), code);
        second.SessionId.Should().Be(result.SessionId);
    }

    [Fact]
    public async Task JoinLobby_MatchIsClosed_SendsNotFound_CodeIsGivenToANewLobby()
    {
        var meta = new MetaWorld(TestLifetime);
        var lobby = await TestLobby.Create(meta.Game, guests: 1);
        var code = lobby.State.Code;

        lobby.RunCountdown();
        var match = meta.Game.Sessions[lobby.Game.SessionId];

        var players = lobby.Users.Select(user => meta.Game.Enter(match.Session.Id, user.BackendId)!).ToList();

        foreach (var user in lobby.Users)
            meta.Game.Disconnect(lobby.Session, user);

        meta.Game.Disconnect(match, players[0]);
        (await meta.Join(Guid.NewGuid(), code)).Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);

        meta.Game.Disconnect(match, players[1]);
        match.Lifetime.IsTerminated.Should().BeTrue();

        (await meta.Join(Guid.NewGuid(), code)).Status.Should().Be(SharedSessionRequests.EntryStatus.NotFound);

        meta.Game.Codes.SetGenerator(() => code);
        var created = await meta.Create(Guid.NewGuid());

        created.Code.Should().Be(code);

        var joined = await meta.Join(Guid.NewGuid(), code);

        joined.Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);
        joined.SessionId.Should().Be(created.SessionId);
    }

    [Fact]
    public async Task JoinLobby_UserWithPlaceReservedBeforeZero_StillEntersTheLobbyAndTheMatch()
    {
        var meta = new MetaWorld(TestLifetime);
        var lobby = await TestLobby.Create(meta.Game, guests: 0);
        var late = Guid.NewGuid();

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(4));

        var reserved = await meta.Join(late, lobby.State.Code);
        reserved.Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);

        lobby.Advance(TimeSpan.FromSeconds(1));
        lobby.Game.SessionId.Should().NotBe(Guid.Empty);

        meta.Game.Enter(reserved.SessionId, late).Should().NotBeNull();
        meta.Game.Enter(lobby.Game.SessionId, late).Should().NotBeNull();
    }

    [Fact]
    public async Task JoinLobby_StartedLobby_ServerIsGone_SendsNotFound()
    {
        var meta = new MetaWorld(TestLifetime);
        var lobby = await TestLobby.Create(meta.Game, guests: 0);

        lobby.RunCountdown();
        meta.Game.RemoveServers();

        var result = await meta.Join(Guid.NewGuid(), lobby.State.Code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.NotFound);
    }

    // A late player: the lobby of the late players, then the entry into the match.

    [Fact]
    public async Task JoinMatch_FromTheLobbyOfLatePlayers_ReservesAPlaceInTheMatch_SendsItsAddress()
    {
        var meta = new MetaWorld(TestLifetime);
        var lobby = await TestLobby.Create(meta.Game, guests: 1);
        var code = lobby.State.Code;
        var late = Guid.NewGuid();

        lobby.RunCountdown();
        var matchId = lobby.Game.SessionId;

        var entered = await meta.Join(late, code);
        meta.Game.Enter(entered.SessionId, late).Should().NotBeNull();

        // only when the game lets a late player in
        meta.Game.EntryPolicy.SetCanEnter(false);
        (await meta.JoinMatch(late, code)).Status.Should().Be(SharedSessionRequests.EntryStatus.Started);
        meta.Game.EntryPolicy.SetCanEnter(true);

        var result = await meta.JoinMatch(late, code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);
        result.SessionId.Should().Be(matchId);
        result.ServerUrl.Should().Be(meta.Game.WebSocketUrl);
        result.Code.Should().Be(code);

        meta.Game.Sessions[matchId].Admission.GetPendingUsers(meta.Game.Time.GetUtcNow().UtcDateTime)
            .Should().Contain(late);

        meta.Game.Enter(matchId, late).Should().NotBeNull();
    }

    [Fact]
    public async Task JoinMatch_CodeOfALobbyThatHasNotStarted_SendsNotFound()
    {
        var meta = new MetaWorld(TestLifetime);
        var lobby = await TestLobby.Create(meta.Game, guests: 0);

        var result = await meta.JoinMatch(Guid.NewGuid(), lobby.State.Code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.NotFound);
        meta.Pipe.Count<SessionPayloads.MatchJoin.Request>().Should().Be(0);
    }

    [Fact]
    public async Task JoinLobby_FullMatch_SendsFull()
    {
        var meta = new MetaWorld(TestLifetime);
        var lobby = await TestLobby.Create(meta.Game, guests: 3);

        lobby.RunCountdown();

        var result = await meta.Join(Guid.NewGuid(), lobby.State.Code);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.Full);
    }

    // The search and the solo session: no code, the game server chooses or builds the match.

    [Fact]
    public async Task Request_SendsOkWithAPublicMatch_SecondUserGetsTheSameMatch()
    {
        var meta = new MetaWorld(TestLifetime);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var found = await meta.Request(first, 0);

        found.Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);
        found.IsLobby.Should().BeFalse();
        found.Code.Should().BeEmpty();
        found.ServerUrl.Should().Be(meta.Game.WebSocketUrl);

        var match = meta.Game.Sessions[found.SessionId];
        match.Session.Type.Should().Be(SessionType.Match);
        match.Session.IsPrivate.Should().BeFalse();
        meta.Game.Enter(found.SessionId, first).Should().NotBeNull();

        (await meta.Request(second, 0)).SessionId.Should().Be(found.SessionId);
        meta.Game.Enter(found.SessionId, second).Should().NotBeNull();
    }

    // The queue goes from the meta through the pipe and SessionEndpoints to the search of the game: the world finds
    // only the matches of the same queue.
    [Fact]
    public async Task Request_InAnotherQueue_GetsAnotherMatch_TheSameQueueGetsTheSameMatch()
    {
        var meta = new MetaWorld(TestLifetime);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        var inQueue = await meta.Request(first, 2);
        var inDefault = await meta.Request(second, 0);
        var inSameQueue = await meta.Request(third, 2);

        inDefault.SessionId.Should().NotBe(inQueue.SessionId);
        inSameQueue.SessionId.Should().Be(inQueue.SessionId);
        meta.Game.Enter(inQueue.SessionId, third).Should().NotBeNull();
        meta.Game.Enter(inDefault.SessionId, second).Should().NotBeNull();
    }

    [Fact]
    public async Task CreateSolo_CreatesMatchForOneAndSendsOkWithoutCode()
    {
        var meta = new MetaWorld(TestLifetime);
        var userId = Guid.NewGuid();

        var result = await meta.CreateSolo(userId);

        result.Status.Should().Be(SharedSessionRequests.EntryStatus.Ok);
        result.ServerUrl.Should().Be(meta.Game.WebSocketUrl);
        result.Code.Should().BeEmpty();

        // the game server reserves the place of the user at the creation: no entry without a reservation
        var match = meta.Game.Sessions[result.SessionId];
        match.Session.IsPrivate.Should().BeTrue();
        match.Options.ExpectedUsers.Should().Be(1);
        match.Options.MaxUsers.Should().Be(1);
        match.Session.Reserve(Guid.NewGuid()).Should().NotBe(SessionReservation.Reserved);
        meta.Game.Enter(result.SessionId, userId).Should().NotBeNull();

        meta.Pipe.Count<SessionPayloads.Create.Request>().Should().Be(1);
    }

    [Fact]
    public void IssueTicket_IsTheTicketOfTheUserIntoTheSession()
    {
        var meta = new MetaWorld(TestLifetime);
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var check = meta.Tickets.Verify(meta.Requester.IssueTicket(userId, sessionId));

        check.Check.Should().Be(SessionTicketCheck.Ok);
        check.Ticket.UserId.Should().Be(userId);
        check.Ticket.SessionId.Should().Be(sessionId);
    }

    [Theory]
    [InlineData(LobbyJoinStatus.Ok, SharedSessionRequests.EntryStatus.Ok)]
    [InlineData(LobbyJoinStatus.NotFound, SharedSessionRequests.EntryStatus.NotFound)]
    [InlineData(LobbyJoinStatus.Full, SharedSessionRequests.EntryStatus.Full)]
    [InlineData(LobbyJoinStatus.Started, SharedSessionRequests.EntryStatus.Started)]
    public void ToWire_MapsEveryStatus_ValuesOnTheWireAreStable(LobbyJoinStatus status, SharedSessionRequests.EntryStatus expected)
    {
        SessionRequester.ToWire(status).Should().Be(expected);
        ((int)status).Should().Be((int)expected);

        // the values the client was built with never move: new ones go to the end
        ((int)SharedSessionRequests.EntryStatus.Ok).Should().Be(0);
        ((int)SharedSessionRequests.EntryStatus.NotFound).Should().Be(1);
        ((int)SharedSessionRequests.EntryStatus.Full).Should().Be(2);
        ((int)SharedSessionRequests.EntryStatus.Started).Should().Be(3);
    }

    [Fact]
    public void ToWire_UnknownStatus_Throws()
    {
        var convert = () => SessionRequester.ToWire((LobbyJoinStatus)100);

        convert.Should().Throw<ArgumentOutOfRangeException>();
    }

    // The meta over the game server of HostingTestWorld: the real SessionRequester sends to the real
    // SessionEndpoints through a pipe that is a direct call (FakeRuntimePipe).
    private class MetaWorld
    {
        public MetaWorld(IReadOnlyLifetime lifetime)
        {
            var messaging = Substitute.For<IMessaging>();
            messaging.RuntimePipe.Returns(Pipe);

            var endpoints = new SessionEndpoints(messaging, Game, Game, Game.Lobbies, Game.Discovery);
            endpoints.OnCoordinatorSetupCompleted(lifetime).GetAwaiter().GetResult();

            Requester = new SessionRequester(
                Game.Discovery,
                GameServerBalancerTests.Create(Game.Discovery),
                messaging,
                Game.Codes,
                Tickets,
                NullLogger<SessionRequester>.Instance);
        }

        public readonly HostingTestWorld Game = new();
        public readonly FakeRuntimePipe Pipe = new();
        public readonly SessionTickets Tickets = TestTickets.Create();
        public readonly SessionRequester Requester;

        public async Task<SharedSessionRequests.Result> Request(Guid userId, int queue)
        {
            return Checked(userId, await Requester.Request(userId, queue));
        }

        public async Task<SharedSessionRequests.Result> CreateSolo(Guid userId)
        {
            return Checked(userId, await Requester.CreateSolo(userId));
        }

        public async Task<SharedSessionRequests.Result> Create(Guid userId)
        {
            return Checked(userId, await Requester.CreateLobby(userId));
        }

        public async Task<SharedSessionRequests.Result> Join(Guid userId, string code)
        {
            return Checked(userId, await Requester.JoinLobby(userId, code));
        }

        public async Task<SharedSessionRequests.Result> JoinMatch(Guid userId, string code)
        {
            return Checked(userId, await Requester.JoinMatch(userId, code));
        }

        // Ok carries the ticket of this user into this session, a refusal carries none.
        private SharedSessionRequests.Result Checked(Guid userId, SharedSessionRequests.Result result)
        {
            if (result.Status != SharedSessionRequests.EntryStatus.Ok)
            {
                result.Ticket.Should().BeEmpty();
                result.SessionId.Should().BeEmpty();
                return result;
            }

            var check = Tickets.Verify(result.Ticket);
            check.Check.Should().Be(SessionTicketCheck.Ok);
            check.Ticket.UserId.Should().Be(userId);
            check.Ticket.SessionId.Should().Be(result.SessionId);

            return result;
        }
    }
}
