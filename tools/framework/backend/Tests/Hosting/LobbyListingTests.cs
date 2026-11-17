using Cluster.Lobbies;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Session;
using Game.Hosting.Collection;
using Game.Lobbies;
using Game.Session;
using Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.Hosting;

// The list of open lobbies as the game server feeds the registry: a lobby reports itself until it starts, then its
// match does; the calls of one code reach the registry in order. Then the lobby of the late players of a running match.
public class LobbyListingTests : LifetimeTest
{
    [Fact]
    public async Task NewLobby_IsClosed_NotListed()
    {
        var lobby = await TestLobby.Create(guests: 1);

        lobby.World.Codes.FindListed(lobby.Code).Should().BeNull();
        lobby.World.Codes.Entries[lobby.Code].Listing.IsOpen.Should().BeFalse();
        lobby.World.Codes.Entries[lobby.Code].Listing.PlayersCount.Should().Be(2);
        lobby.World.Codes.Entries[lobby.Code].Listing.MaxPlayers.Should().Be(4);
    }

    [Fact]
    public async Task LeaderOpens_Listed_LeaderCloses_NotListed()
    {
        var lobby = await TestLobby.Create(guests: 1);

        lobby.WriteVisibility(true);

        var listed = lobby.World.Codes.FindListed(lobby.Code);
        listed.Should().NotBeNull();
        listed!.PlayersCount.Should().Be(2);
        listed.LeaderUserId.Should().Be(lobby.Users[0].BackendId);
        listed.IsRunning.Should().BeFalse();

        lobby.WriteVisibility(false);

        lobby.World.Codes.FindListed(lobby.Code).Should().BeNull();
    }

    [Fact]
    public async Task NotLeader_CantOpen()
    {
        var lobby = await TestLobby.Create(guests: 1);

        lobby.WriteVisibility(true, lobby.Users[1]);

        lobby.World.Codes.FindListed(lobby.Code).Should().BeNull();
    }

    [Fact]
    public async Task FullLobby_NotListed_OneLeaves_ListedAgain()
    {
        var lobby = await TestLobby.Create(guests: 3);

        lobby.WriteVisibility(true);
        lobby.World.Codes.FindListed(lobby.Code).Should().BeNull();

        lobby.World.Disconnect(lobby.Session, lobby.Users[3]);

        lobby.World.Codes.FindListed(lobby.Code)!.PlayersCount.Should().Be(3);
    }

    [Fact]
    public async Task LeaderLeaves_LeadershipAndVisibilityPass_NewLeaderCanClose()
    {
        var lobby = await TestLobby.Create(guests: 2);

        lobby.WriteVisibility(true);
        lobby.World.Disconnect(lobby.Session, lobby.Users[0]);

        var listed = lobby.World.Codes.FindListed(lobby.Code);
        listed!.LeaderUserId.Should().Be(lobby.Users[1].BackendId);
        listed.PlayersCount.Should().Be(2);

        lobby.WriteVisibility(false, lobby.Users[2]);
        lobby.World.Codes.FindListed(lobby.Code).Should().NotBeNull();

        lobby.WriteVisibility(false, lobby.Users[1]);
        lobby.World.Codes.FindListed(lobby.Code).Should().BeNull();
    }

    [Fact]
    public async Task EmptyLobby_CodeReleased_NotListed()
    {
        var lobby = await TestLobby.Create(guests: 0);
        var code = lobby.Code;

        lobby.WriteVisibility(true);
        lobby.World.Disconnect(lobby.Session, lobby.Users[0]);

        lobby.World.Codes.Entries.Should().NotContainKey(code);
        lobby.World.Codes.Calls.Last().Should().Be($"Release {code}");
    }

    [Fact]
    public async Task OpenLobbyStarts_MatchIsListedAsRunning_WithItsPlayers()
    {
        var lobby = await TestLobby.Create(guests: 1);
        var code = lobby.Code;

        lobby.WriteVisibility(true);
        lobby.RunCountdown();

        var match = lobby.World.Sessions[lobby.Game.SessionId];
        match.MatchListing().Should().NotBeNull();

        // nobody has entered the match yet
        lobby.World.Codes.FindListed(code).Should().BeNull();

        var players = lobby.Users.Select(user => lobby.World.Enter(match.Session.Id, user.BackendId)!).ToList();

        foreach (var user in lobby.Users)
            lobby.World.Disconnect(lobby.Session, user);

        lobby.Session.Lifetime.IsTerminated.Should().BeTrue();

        var listed = lobby.World.Codes.FindListed(code);
        listed.Should().NotBeNull();
        listed!.IsRunning.Should().BeTrue();
        listed.PlayersCount.Should().Be(2);
        listed.MaxPlayers.Should().Be(4);
        listed.LeaderUserId.Should().Be(players[0].BackendId);

        // the mark goes before every report of the match, the release is the last call
        var calls = lobby.World.Codes.Calls.ToList();
        calls.IndexOf($"Mark {code}").Should().BeGreaterThan(-1);
        calls.Skip(calls.IndexOf($"Mark {code}")).Should().OnlyContain(call => call == $"Listing {code}" || call == $"Mark {code}");

        foreach (var player in players)
            lobby.World.Disconnect(match, player);

        match.Lifetime.IsTerminated.Should().BeTrue();
        lobby.World.Codes.Entries.Should().NotContainKey(code);
        lobby.World.Codes.Calls.Last().Should().Be($"Release {code}");
    }

    [Fact]
    public async Task ClosedLobbyStarts_MatchIsNotListed()
    {
        var lobby = await TestLobby.Create(guests: 1);
        var code = lobby.Code;

        lobby.RunCountdown();

        var match = lobby.World.Sessions[lobby.Game.SessionId];
        lobby.World.Enter(match.Session.Id, lobby.Users[0].BackendId);

        lobby.World.Codes.Entries[code].Listing.IsRunning.Should().BeTrue();
        lobby.World.Codes.FindListed(code).Should().BeNull();
    }

    [Fact]
    public async Task SlowMark_ReportsOfTheMatchWaitForIt()
    {
        var lobby = await TestLobby.Create(guests: 0);
        var code = lobby.Code;

        lobby.WriteVisibility(true);
        lobby.World.Codes.HoldMarkStarted(new TaskCompletionSource());
        lobby.RunCountdown();

        var match = lobby.World.Sessions[lobby.Game.SessionId];
        lobby.World.Enter(match.Session.Id, lobby.Users[0].BackendId);

        // the player in the match is not reported before the mark
        lobby.World.Codes.Entries[code].Listing.PlayersCount.Should().Be(0);
        lobby.World.Codes.Calls.Should().NotContain($"Mark {code}");

        lobby.World.Codes.MarkStartedGate.SetResult();
        await TestWait.Until(() => lobby.World.Codes.Entries[code].Listing.PlayersCount == 1);

        lobby.World.Codes.FindListed(code)!.PlayersCount.Should().Be(1);
    }

    [Fact]
    public void SessionContainer_MatchOfALobby_HasTheListingService()
    {
        var lifetime = new Lifetime(TestLifetime);

        var options = new SessionCreateOptions
        {
            Type = SessionType.Match,
            ExpectedUsers = 0,
            MaxUsers = 4,
            IsPrivate = true
        };

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(Substitute.For<ILobbyListings>());

        services.AddSessionServices(new SessionContainerData
        {
            Id = options.Id,
            Lifetime = lifetime,
            ExpectedUsers = options.ExpectedUsers
        }, options);

        services.AddMatchListingService(new MatchListingInfo
        {
            LobbySessionId = Guid.NewGuid(),
            IsOpen = true,
            MaxUsers = 4
        });

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IService>()
                .Select(service => service.Key)
                .Should().Equal(MatchListingService.ServiceKey);

        lifetime.Terminate();
    }

    [Fact]
    public void MatchListingService_WithoutALobby_Throws()
    {
        var create = () => new MatchListingService(
            Substitute.For<ISessionOwner>(),
            new SessionUsers(),
            Substitute.For<ILobbyListings>(),
            new MatchListingInfo
            {
                LobbySessionId = Guid.Empty,
                IsOpen = true,
                MaxUsers = 4
            },
            NullLogger<MatchListingService>.Instance);

        create.Should().Throw<InvalidOperationException>();
    }

    // The lobby of the late players.

    [Fact]
    public async Task LobbyOfLatePlayers_IsRunning_NoStartNoVisibility_ReportsNothing()
    {
        var world = new HostingTestWorld();
        var (lobby, match, code) = await StartMatch(world, isOpen: true);

        var late = Guid.NewGuid();
        var joined = world.Lobbies.Join(lobby.Session.Session.Id, match.Session.Id, code, late);

        joined.Status.Should().Be(LobbyJoinStatus.Ok);

        var running = world.Sessions[joined.SessionId];
        var user = world.Enter(joined.SessionId, late)!;
        var calls = world.Codes.Calls.Count;

        running.LobbyInfo().RunningMatchId.Should().Be(match.Session.Id);
        running.LobbyInfo().LobbySessionId.Should().Be(lobby.Session.Session.Id);

        var state = running.LobbyService().Read<LobbyState>(SharedLobbyService.LobbyPropertyId);
        state.IsRunning.Should().BeTrue();
        state.IsRunOver.Should().BeFalse();
        state.Code.Should().Be(code);

        running.LobbyService().Properties[SharedLobbyService.StartPropertyId].CanBeWrittenBy(user).Should().BeFalse();
        running.LobbyService().Properties[SharedLobbyService.VisibilityPropertyId].CanBeWrittenBy(user).Should().BeFalse();

        world.Disconnect(running, user);

        running.Lifetime.IsTerminated.Should().BeTrue();
        world.Codes.Calls.Count.Should().Be(calls);
        world.Codes.Entries.Should().ContainKey(code);
    }

    [Fact]
    public async Task LatePlayers_OneLobbyPerMatch_ClosedWithItsLastPlayer_ThenANewOne()
    {
        var world = new HostingTestWorld();
        var (lobby, match, code) = await StartMatch(world, isOpen: true, guests: 1);
        var lobbyId = lobby.Session.Session.Id;

        var first = world.Lobbies.Join(lobbyId, match.Session.Id, code, Guid.NewGuid());
        var second = world.Lobbies.Join(lobbyId, match.Session.Id, code, Guid.NewGuid());

        second.SessionId.Should().Be(first.SessionId);

        var running = world.Sessions[first.SessionId];
        world.Time.Advance(HostingTestWorld.JoinTimeout);
        running.Queue.Drain();

        running.Lifetime.IsTerminated.Should().BeTrue();

        var third = world.Lobbies.Join(lobbyId, match.Session.Id, code, Guid.NewGuid());

        third.Status.Should().Be(LobbyJoinStatus.Ok);
        third.SessionId.Should().NotBe(first.SessionId);
    }

    [Fact]
    public async Task LatePlayers_LastPlace_SecondGetsFull()
    {
        var world = new HostingTestWorld();
        var (lobby, match, code) = await StartMatch(world, isOpen: true, guests: 2);
        var lobbyId = lobby.Session.Session.Id;

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        world.Lobbies.Join(lobbyId, match.Session.Id, code, first).Status.Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.Join(lobbyId, match.Session.Id, code, second).Status.Should().Be(LobbyJoinStatus.Ok);

        world.Lobbies.JoinMatch(lobbyId, match.Session.Id, first).Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinMatch(lobbyId, match.Session.Id, second).Should().Be(LobbyJoinStatus.Full);

        // a full match lets nobody into the lobby of its late players either
        world.Lobbies.Join(lobbyId, match.Session.Id, code, Guid.NewGuid()).Status.Should().Be(LobbyJoinStatus.Full);

        world.Enter(match.Session.Id, first).Should().NotBeNull();
        world.Codes.FindListed(code).Should().BeNull();
    }

    // The game decides when a running match lets a late player in (ILobbyEntryPolicy).
    [Fact]
    public async Task JoinMatch_EntryRefusedByTheGame_SendsStarted_UntilItIsAllowed()
    {
        var world = new HostingTestWorld();
        var (lobby, match, code) = await StartMatch(world, isOpen: true);
        var lobbyId = lobby.Session.Session.Id;
        var late = Guid.NewGuid();

        world.Lobbies.Join(lobbyId, match.Session.Id, code, late).Status.Should().Be(LobbyJoinStatus.Ok);

        world.EntryPolicy.SetCanEnter(false);
        world.Lobbies.JoinMatch(lobbyId, match.Session.Id, late).Should().Be(LobbyJoinStatus.Started);
        match.Admission.GetPendingUsers(world.Time.GetUtcNow().UtcDateTime).Should().BeEmpty();

        world.EntryPolicy.SetCanEnter(true);
        world.Lobbies.JoinMatch(lobbyId, match.Session.Id, late).Should().Be(LobbyJoinStatus.Ok);
        match.Admission.GetPendingUsers(world.Time.GetUtcNow().UtcDateTime).Should().Equal(late);
    }

    [Fact]
    public async Task Join_MatchOverForTheGame_SendsNotFound_LobbyOfLatePlayersIsNotCreated()
    {
        var world = new HostingTestWorld();
        var (lobby, match, code) = await StartMatch(world, isOpen: true);
        var sessions = world.Sessions.Count;

        world.EntryPolicy.MarkMatchOver(match.Session.Id);

        world.Lobbies.Join(lobby.Session.Session.Id, match.Session.Id, code, Guid.NewGuid()).Status
             .Should().Be(LobbyJoinStatus.NotFound);
        world.Lobbies.JoinMatch(lobby.Session.Session.Id, match.Session.Id, Guid.NewGuid()).Should().Be(LobbyJoinStatus.NotFound);
        world.Sessions.Should().HaveCount(sessions);
    }

    // A late player lost its client (the socket stays open) and comes back by the code while the others play on. Its
    // stale connection neither fills the match nor stays in it.
    [Fact]
    public async Task LatePlayerWithStaleConnection_JoinsAgain_StaleDropped()
    {
        var world = new HostingTestWorld();
        var (lobby, match, code) = await StartMatch(world, isOpen: true, guests: 2);
        var lobbyId = lobby.Session.Session.Id;
        var late = Guid.NewGuid();

        world.Lobbies.Join(lobbyId, match.Session.Id, code, late).Status.Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinMatch(lobbyId, match.Session.Id, late).Should().Be(LobbyJoinStatus.Ok);
        var stale = world.Enter(match.Session.Id, late)!;
        var players = match.Users.Count;

        world.Lobbies.Join(lobbyId, match.Session.Id, code, late).Status.Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinMatch(lobbyId, match.Session.Id, late).Should().Be(LobbyJoinStatus.Ok);
        match.Queue.Drain();

        TestSession.Connection(stale).ForceDisconnects.Should().Be(1);
        match.Users.Should().NotContain(stale);

        world.Enter(match.Session.Id, late).Should().NotBeNull();
        match.Users.Count.Should().Be(players);
        match.Lifetime.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public async Task MatchClosedWhileWaiting_JoinMatchSendsNotFound()
    {
        var world = new HostingTestWorld();
        var (lobby, match, code) = await StartMatch(world, isOpen: false);
        var lobbyId = lobby.Session.Session.Id;
        var late = Guid.NewGuid();

        world.Lobbies.Join(lobbyId, match.Session.Id, code, late).Status.Should().Be(LobbyJoinStatus.Ok);

        foreach (var user in match.Users.ToList())
            world.Disconnect(match, user);

        match.Lifetime.IsTerminated.Should().BeTrue();

        world.Lobbies.JoinMatch(lobbyId, match.Session.Id, late).Should().Be(LobbyJoinStatus.NotFound);
    }

    [Fact]
    public async Task JoinLate_PublicMatch_LobbyWithoutACode()
    {
        var world = new HostingTestWorld();
        var match = world.CreatePublicMatch();
        var player = match.ReserveAndEnter();
        var late = Guid.NewGuid();

        var joined = world.Lobbies.JoinLate(match.Session.Id, late);

        joined.Status.Should().Be(LobbyJoinStatus.Ok);

        var running = world.Sessions[joined.SessionId];
        running.LobbyInfo().Code.Should().BeEmpty();
        running.LobbyInfo().LobbySessionId.Should().Be(Guid.Empty);
        running.LobbyInfo().RunningMatchId.Should().Be(match.Session.Id);
        running.LobbyService().Read<LobbyState>(SharedLobbyService.LobbyPropertyId).IsRunning.Should().BeTrue();
        world.Enter(joined.SessionId, late).Should().NotBeNull();

        world.Disconnect(match, player);
        match.Lifetime.IsTerminated.Should().BeTrue();

        world.Lobbies.JoinLate(match.Session.Id, Guid.NewGuid()).Status.Should().Be(LobbyJoinStatus.NotFound);
    }

    // The lobby of the late players holds no place in the match: it fills up on its own.
    [Fact]
    public void LobbyOfLatePlayers_IsFull_NextPlayerGetsFull()
    {
        var world = new HostingTestWorld(maxUsers: 2);
        var match = world.CreatePublicMatch();
        match.ReserveAndEnter();

        world.Lobbies.JoinLate(match.Session.Id, Guid.NewGuid()).Status.Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinLate(match.Session.Id, Guid.NewGuid()).Status.Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinLate(match.Session.Id, Guid.NewGuid()).Status.Should().Be(LobbyJoinStatus.Full);
    }

    // The place checked by the first look is gone when it is taken.
    [Fact]
    public void JoinMatch_PlaceTakenBetweenTheCheckAndTheReservation_Full_OrNotFoundWhenClosed()
    {
        var world = new HostingTestWorld();
        var matchId = Guid.NewGuid();
        var match = Substitute.For<ISession>();
        match.Type.Returns(SessionType.Match);
        match.HasFreePlace(Arg.Any<Guid>()).Returns(true);
        match.Reserve(Arg.Any<Guid>()).Returns(SessionReservation.Full, SessionReservation.Closed);
        match.IsClosed.Returns(false);

        var collection = Substitute.For<ISessionsCollection>();

        collection.TryGet(matchId, out Arg.Any<ISession>()!).Returns(call => {
            call[1] = match;
            return true;
        });

        var lobbies = Lobbies(world, world, collection);

        lobbies.JoinMatch(Guid.Empty, matchId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Full);
        lobbies.JoinMatch(Guid.Empty, matchId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.NotFound);
    }

    // The framework never seals a match: a sealed one is an unexpected state, not a full match.
    [Fact]
    public void JoinMatch_MatchSealed_Throws()
    {
        var world = new HostingTestWorld();
        var matchId = Guid.NewGuid();
        var match = Substitute.For<ISession>();
        match.Type.Returns(SessionType.Match);
        match.HasFreePlace(Arg.Any<Guid>()).Returns(true);
        match.Reserve(Arg.Any<Guid>()).Returns(SessionReservation.Sealed);
        match.IsClosed.Returns(false);

        var collection = Substitute.For<ISessionsCollection>();

        collection.TryGet(matchId, out Arg.Any<ISession>()!).Returns(call => {
            call[1] = match;
            return true;
        });

        var lobbies = Lobbies(world, world, collection);

        var join = () => lobbies.JoinMatch(Guid.Empty, matchId, Guid.NewGuid());

        join.Should().Throw<InvalidOperationException>();
    }

    // A sealed lobby of the late players takes nobody any more: it is replaced like a closing one, never Full.
    [Fact]
    public void JoinLate_LobbyOfLatePlayersSealed_NewLobby()
    {
        var world = new HostingTestWorld();
        var match = world.CreatePublicMatch();
        match.ReserveAndEnter();

        var first = world.Lobbies.JoinLate(match.Session.Id, Guid.NewGuid());
        first.Status.Should().Be(LobbyJoinStatus.Ok);
        world.Sessions[first.SessionId].Admission.Seal();

        var second = world.Lobbies.JoinLate(match.Session.Id, Guid.NewGuid());

        second.Status.Should().Be(LobbyJoinStatus.Ok);
        second.SessionId.Should().NotBe(first.SessionId);
    }

    // A lobby of the late players closed under the reservation is replaced once; the old one ending later does not
    // take the new one out of the map.
    [Fact]
    public void LobbyOfLatePlayersClosedUnderTheReservation_IsReplaced_OldOneEndsLater_NewOneStays()
    {
        var world = new HostingTestWorld();
        var match = world.CreatePublicMatch();
        var closedLifetime = new Lifetime(TestLifetime);
        var closed = LateLobby(closedLifetime, isClosed: true, reserves: false);
        var replacement = LateLobby(new Lifetime(TestLifetime), isClosed: false, reserves: true);
        var factory = Substitute.For<ILobbyFactory>();
        factory.Create(Arg.Any<LobbyCreation>()).Returns(closed, replacement);

        var lobbies = Lobbies(world, factory, world.Collection);

        lobbies.JoinLate(match.Session.Id, Guid.NewGuid()).SessionId.Should().Be(replacement.Id);

        closedLifetime.Terminate();

        lobbies.JoinLate(match.Session.Id, Guid.NewGuid()).SessionId.Should().Be(replacement.Id);
        factory.ReceivedCalls().Should().HaveCount(2);
    }

    [Fact]
    public void LobbyOfLatePlayersClosedUnderTheReservationTwice_Throws()
    {
        var world = new HostingTestWorld();
        var match = world.CreatePublicMatch();
        var factory = Substitute.For<ILobbyFactory>();

        factory.Create(Arg.Any<LobbyCreation>())
               .Returns(_ => LateLobby(new Lifetime(TestLifetime), isClosed: true, reserves: false));

        var lobbies = Lobbies(world, factory, world.Collection);

        var join = () => lobbies.JoinLate(match.Session.Id, Guid.NewGuid());

        join.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LobbyFactoryBuildsAMatchForTheLatePlayers_Throws()
    {
        var world = new HostingTestWorld();
        var match = world.CreatePublicMatch();
        var built = world.CreatePublicMatch().Session;
        var factory = Substitute.For<ILobbyFactory>();
        factory.Create(Arg.Any<LobbyCreation>()).Returns(built);

        var lobbies = Lobbies(world, factory, world.Collection);

        var join = () => lobbies.JoinLate(match.Session.Id, Guid.NewGuid());

        join.Should().Throw<InvalidOperationException>();
    }

    private static LobbySessions Lobbies(HostingTestWorld world, ILobbyFactory factory, ISessionsCollection collection)
    {
        return new LobbySessions(
            factory,
            collection,
            world.Codes,
            world.Leases,
            world.EntryPolicy,
            world.Options,
            world.Discovery,
            NullLogger<LobbySessions>.Instance);
    }

    private static ISession LateLobby(Lifetime lifetime, bool isClosed, bool reserves)
    {
        var lobby = Substitute.For<ISession>();
        lobby.Id.Returns(Guid.NewGuid());
        lobby.Type.Returns(SessionType.Lobby);
        lobby.Lifetime.Returns(lifetime);
        lobby.IsClosed.Returns(isClosed);
        lobby.Reserve(Arg.Any<Guid>()).Returns(reserves == true ? SessionReservation.Reserved : SessionReservation.Closed);

        return lobby;
    }

    // An open lobby with its players moved to the started match: the lobby session is closed.
    private static async Task<(TestLobby Lobby, TestSession Match, string Code)> StartMatch(
        HostingTestWorld world,
        bool isOpen,
        int guests = 1)
    {
        var lobby = await TestLobby.Create(world, guests);
        var code = lobby.Code;

        if (isOpen == true)
            lobby.WriteVisibility(true);

        lobby.RunCountdown();

        var match = world.Sessions[lobby.Game.SessionId];

        foreach (var user in lobby.Users)
        {
            world.Enter(match.Session.Id, user.BackendId).Should().NotBeNull();
            world.Disconnect(lobby.Session, user);
        }

        return (lobby, match, code);
    }

    // A bot of the match takes no place: it is neither a player of the listing nor its leader.
    [Fact]
    public async Task MatchWithABot_BotIsNotAPlayerNorTheLeader()
    {
        var lobby = await TestLobby.Create(guests: 1);
        var code = lobby.Code;

        lobby.WriteVisibility(true);
        lobby.RunCountdown();

        var match = lobby.World.Sessions[lobby.Game.SessionId];
        match.AddBot();
        var player = lobby.World.Enter(match.Session.Id, lobby.Users[0].BackendId)!;

        var listed = lobby.World.Codes.FindListed(code);
        listed.Should().NotBeNull();
        listed!.PlayersCount.Should().Be(1);
        listed.LeaderUserId.Should().Be(player.BackendId);
    }
}
