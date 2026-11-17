using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Session;
using Game.Hosting;
using Game.Lobbies;
using Game.Session;
using MemoryPack;
using Shared;
using Xunit;

namespace Framework.Tests.Hosting;

// The start of a lobby: the leader writes the start flag, the backend counts down and at zero creates the
// match with a place for every user of the lobby. Real sessions on manual time and queues: HostingTestWorld.
public class LobbyStartTests
{
    [Fact]
    public async Task Start_CountsDownFromFiveToZero_OneStepASecond()
    {
        var lobby = await TestLobby.Create(guests: 1);

        lobby.Countdown.IsActive.Should().BeFalse();

        lobby.WriteStart(true);

        lobby.Countdown.IsActive.Should().BeTrue();
        lobby.Countdown.SecondsLeft.Should().Be(5);

        for (var expected = 4; expected >= 0; expected--)
        {
            // nothing changes before the whole second has passed
            lobby.Advance(TimeSpan.FromMilliseconds(900));
            lobby.Countdown.SecondsLeft.Should().Be(expected + 1);

            lobby.Advance(TimeSpan.FromMilliseconds(100));
            lobby.Countdown.SecondsLeft.Should().Be(expected);
            lobby.Countdown.IsActive.Should().BeTrue();
        }

        lobby.Game.SessionId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Start_NoMatchUntilZero()
    {
        var lobby = await TestLobby.Create(guests: 1);

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(4));

        lobby.Countdown.SecondsLeft.Should().Be(1);
        lobby.Game.SessionId.Should().Be(Guid.Empty);
        lobby.World.Sessions.Should().HaveCount(1);
    }

    [Fact]
    public async Task Cancel_StopsCountdown_NoMatchIsCreated_StartAgainCountsFromFive()
    {
        var lobby = await TestLobby.Create(guests: 1);

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(2));
        lobby.Countdown.SecondsLeft.Should().Be(3);

        lobby.WriteStart(false);

        lobby.Countdown.IsActive.Should().BeFalse();

        lobby.Advance(TimeSpan.FromSeconds(10));

        lobby.Countdown.IsActive.Should().BeFalse();
        lobby.Game.SessionId.Should().Be(Guid.Empty);
        lobby.World.Sessions.Should().HaveCount(1);

        lobby.WriteStart(true);

        lobby.Countdown.IsActive.Should().BeTrue();
        lobby.Countdown.SecondsLeft.Should().Be(5);

        // the step of the cancelled countdown is dead: one second - one step
        lobby.Advance(TimeSpan.FromSeconds(1));
        lobby.Countdown.SecondsLeft.Should().Be(4);
    }

    [Fact]
    public async Task CancelAndStartWithinOneSecond_OnlyTheNewCountdownSteps()
    {
        var lobby = await TestLobby.Create(guests: 0);

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromMilliseconds(500));
        lobby.WriteStart(false);
        lobby.WriteStart(true);

        // the step of the first countdown is due here
        lobby.Advance(TimeSpan.FromMilliseconds(500));
        lobby.Countdown.SecondsLeft.Should().Be(5);

        lobby.Advance(TimeSpan.FromMilliseconds(500));
        lobby.Countdown.SecondsLeft.Should().Be(4);
    }

    [Fact]
    public async Task LeaderLeaves_DuringCountdown_StartAndCountdownAreReset_NewLeaderStarts()
    {
        var lobby = await TestLobby.Create(guests: 1);

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(2));

        lobby.World.Disconnect(lobby.Session, lobby.Users[0]);

        lobby.State.OwnerIndex.Should().Be(lobby.Users[1].Index);
        lobby.Start.IsStarting.Should().BeFalse();
        lobby.Countdown.IsActive.Should().BeFalse();

        lobby.Advance(TimeSpan.FromSeconds(10));
        lobby.Game.SessionId.Should().Be(Guid.Empty);

        lobby.WriteStart(true);
        lobby.Countdown.SecondsLeft.Should().Be(5);

        lobby.Advance(TimeSpan.FromSeconds(5));
        lobby.Game.SessionId.Should().NotBe(Guid.Empty);
        lobby.World.MatchLeaderId.Should().Be(lobby.Users[1].BackendId);
    }

    [Fact]
    public async Task Zero_TheMatchIsCreatedForTheLeader()
    {
        var lobby = await TestLobby.Create(guests: 2);

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(5));

        lobby.Game.SessionId.Should().NotBe(Guid.Empty);
        lobby.World.MatchLeaderId.Should().Be(lobby.Users[0].BackendId);
    }

    [Fact]
    public async Task GuestLeaves_DuringCountdown_CountdownGoesOn()
    {
        var lobby = await TestLobby.Create(guests: 1);

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(2));

        lobby.World.Disconnect(lobby.Session, lobby.Users[1]);

        lobby.Start.IsStarting.Should().BeTrue();
        lobby.Countdown.IsActive.Should().BeTrue();
        lobby.Countdown.SecondsLeft.Should().Be(3);

        lobby.Advance(TimeSpan.FromSeconds(3));
        lobby.Game.SessionId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Join_DuringCountdown_IsLetIn_CountdownGoesOn_NewUserGetsTheCurrentNumber()
    {
        var lobby = await TestLobby.Create(guests: 0);
        var guest = Guid.NewGuid();

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(2));

        lobby.World.Lobbies.JoinLobby(lobby.Session.Session.Id, guest).Should().Be(LobbyJoinStatus.Ok);
        lobby.World.Enter(lobby.Session.Session.Id, guest).Should().NotBeNull();

        lobby.Countdown.IsActive.Should().BeTrue();
        lobby.Countdown.SecondsLeft.Should().Be(3);

        // what the new client gets in its GetResponse
        var overview = lobby.Service.CreateOverview()
                            .Properties.Single(property => property.PropertyId == SharedLobbyService.CountdownPropertyId);

        MemoryPackSerializer.Deserialize<LobbyCountdownState>(overview.Value)!.SecondsLeft.Should().Be(3);
    }

    [Fact]
    public async Task Zero_CreatesPrivateMatchOfTheConfiguredPlaces_OnTheSameServer()
    {
        var lobby = await TestLobby.Create(guests: 1);

        lobby.RunCountdown();

        var match = lobby.World.Sessions[lobby.Game.SessionId];

        lobby.Game.ServerUrl.Should().Be("ws://localhost:7102");
        lobby.Game.PlayersCount.Should().Be(2);
        match.Session.Type.Should().Be(SessionType.Match);
        match.Session.IsPrivate.Should().BeTrue();
        match.Options.MaxUsers.Should().Be(4);
        match.Options.ExpectedUsers.Should().Be(0);
        lobby.World.Collection.TryGet(lobby.Game.SessionId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Zero_ReservesPlaceForEveryUserOfTheLobby()
    {
        var lobby = await TestLobby.Create(guests: 3);

        lobby.RunCountdown();

        var match = lobby.World.Sessions[lobby.Game.SessionId];

        // four reservations: the match is full for anybody else
        match.Session.Reserve(Guid.NewGuid()).Should().NotBe(SessionReservation.Reserved);

        // the reservations hold the empty match until JoinTimeout
        lobby.World.Time.Advance(HostingTestWorld.JoinTimeout - TimeSpan.FromSeconds(1));
        match.Queue.Drain();
        match.Lifetime.IsTerminated.Should().BeFalse();

        foreach (var user in lobby.Users)
            lobby.World.Enter(match.Session.Id, user.BackendId).Should().NotBeNull();

        match.Users.Count.Should().Be(4);
        match.Session.Reserve(Guid.NewGuid()).Should().NotBe(SessionReservation.Reserved);
    }

    [Fact]
    public async Task Zero_UserWithReservedPlaceInTheLobby_GetsPlaceInTheMatch()
    {
        var lobby = await TestLobby.Create(guests: 2);
        var late = Guid.NewGuid();

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(4));

        // reserved by the code, not connected yet
        lobby.World.Lobbies.JoinLobby(lobby.Session.Session.Id, late).Should().Be(LobbyJoinStatus.Ok);

        lobby.Advance(TimeSpan.FromSeconds(1));

        var match = lobby.World.Sessions[lobby.Game.SessionId];

        // 3 connected + 1 on its way: no place is left
        lobby.Game.PlayersCount.Should().Be(4);
        match.Session.Reserve(Guid.NewGuid()).Should().NotBe(SessionReservation.Reserved);

        // the late user still enters the sealed lobby and reads the address of the match there
        lobby.World.Enter(lobby.Session.Session.Id, late).Should().NotBeNull();
        lobby.World.Enter(match.Session.Id, late).Should().NotBeNull();
    }

    [Fact]
    public async Task Zero_NobodyEntersTheMatch_MatchClosesAfterJoinTimeout()
    {
        var lobby = await TestLobby.Create(guests: 1);

        lobby.RunCountdown();

        var match = lobby.World.Sessions[lobby.Game.SessionId];

        lobby.World.Time.Advance(HostingTestWorld.JoinTimeout);
        match.Queue.Drain();

        match.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public async Task Zero_SealsTheLobby_JoinByCodeIsStarted_NoPlaceInTheMatchIsGiven()
    {
        var lobby = await TestLobby.Create(guests: 1);
        var stranger = Guid.NewGuid();

        lobby.RunCountdown();

        lobby.Session.Session.IsSealed.Should().BeTrue();
        lobby.World.Lobbies.JoinLobby(lobby.Session.Session.Id, stranger).Should().Be(LobbyJoinStatus.Started);

        // a user of the lobby asking again is refused too: the lobby is over
        lobby.World.Lobbies.JoinLobby(lobby.Session.Session.Id, lobby.Users[1].BackendId).Should().Be(LobbyJoinStatus.Started);

        // the refusal reserved nothing: two places of the match are taken by the users of the lobby, two are free
        var match = lobby.World.Sessions[lobby.Game.SessionId];

        match.Admission.GetPendingUsers(lobby.World.Time.GetUtcNow().UtcDateTime)
             .Should().BeEquivalentTo(lobby.Users.Select(user => user.BackendId));
    }

    [Fact]
    public async Task CancelAfterZero_IsIgnored_MatchStays()
    {
        var lobby = await TestLobby.Create(guests: 1);

        lobby.RunCountdown();
        var matchId = lobby.Game.SessionId;

        lobby.WriteStart(false);
        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(10));

        lobby.Game.SessionId.Should().Be(matchId);
        lobby.Countdown.IsActive.Should().BeTrue();
        lobby.Countdown.SecondsLeft.Should().Be(0);
        lobby.World.Sessions.Should().HaveCount(2);
    }

    [Fact]
    public async Task UsersLeaveForTheMatch_LobbyCloses_CodeStaysWithTheMatch_StartIsNotReset()
    {
        var lobby = await TestLobby.Create(guests: 1);
        var code = lobby.State.Code;

        lobby.RunCountdown();
        var matchId = lobby.Game.SessionId;

        // the leader leaves first: the leader changes after zero
        lobby.World.Disconnect(lobby.Session, lobby.Users[0]);

        lobby.Game.SessionId.Should().Be(matchId);
        lobby.Countdown.SecondsLeft.Should().Be(0);
        lobby.Session.Lifetime.IsTerminated.Should().BeFalse();

        lobby.World.Disconnect(lobby.Session, lobby.Users[1]);

        lobby.Session.Lifetime.IsTerminated.Should().BeTrue();
        lobby.World.Sessions[matchId].Lifetime.IsTerminated.Should().BeFalse();

        // the closed lobby has not released the code: the match holds it
        var entry = await lobby.World.Codes.Find(code);

        entry.Should().NotBeNull();
        entry!.SessionId.Should().Be(lobby.Session.Session.Id);
        entry.MatchSessionId.Should().Be(matchId);
    }

    [Fact]
    public async Task Zero_MarksTheCodeAsStartedWithTheMatch()
    {
        var lobby = await TestLobby.Create(guests: 1);
        var code = lobby.State.Code;

        (await lobby.World.Codes.Find(code))!.MatchSessionId.Should().Be(Guid.Empty);

        lobby.RunCountdown();

        (await lobby.World.Codes.Find(code))!.MatchSessionId.Should().Be(lobby.Game.SessionId);
    }

    [Fact]
    public async Task CancelledCountdown_CodeIsNotMarked_JoinIsOk()
    {
        var lobby = await TestLobby.Create(guests: 1);
        var code = lobby.State.Code;

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(3));
        lobby.WriteStart(false);
        lobby.Advance(TimeSpan.FromSeconds(10));

        (await lobby.World.Codes.Find(code))!.MatchSessionId.Should().Be(Guid.Empty);
        lobby.Session.Session.IsSealed.Should().BeFalse();
        lobby.World.Lobbies.JoinLobby(lobby.Session.Session.Id, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Ok);
    }

    [Fact]
    public async Task MatchCloses_LastPlayerLeft_CodeIsReleased_CanBeGivenToANewLobby()
    {
        var lobby = await TestLobby.Create(guests: 1);
        var code = lobby.State.Code;

        lobby.RunCountdown();
        var match = lobby.World.Sessions[lobby.Game.SessionId];

        var players = lobby.Users.Select(user => lobby.World.Enter(match.Session.Id, user.BackendId)!).ToList();

        foreach (var user in lobby.Users)
            lobby.World.Disconnect(lobby.Session, user);

        lobby.Session.Lifetime.IsTerminated.Should().BeTrue();

        // one player leaves: the match goes on, the code is still taken
        lobby.World.Disconnect(match, players[0]);

        match.Lifetime.IsTerminated.Should().BeFalse();
        (await lobby.World.Codes.Find(code))!.MatchSessionId.Should().Be(match.Session.Id);

        lobby.World.Disconnect(match, players[1]);

        match.Lifetime.IsTerminated.Should().BeTrue();
        (await lobby.World.Codes.Find(code)).Should().BeNull();

        lobby.World.Codes.SetGenerator(() => code);
        var next = await lobby.World.Lobbies.Create(Guid.NewGuid());

        next.Code.Should().Be(code);
        (await lobby.World.Codes.Find(code))!.MatchSessionId.Should().Be(Guid.Empty);
        lobby.World.Lobbies.JoinLobby(next.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Ok);
    }

    [Fact]
    public async Task StartedCode_IsNotGivenToANewLobby_WhileTheMatchIsGoing()
    {
        var lobby = await TestLobby.Create(guests: 0);
        var code = lobby.State.Code;

        lobby.RunCountdown();
        lobby.World.Disconnect(lobby.Session, lobby.Users[0]);

        var generated = new Queue<string>([code, code == "ZZZZZZ" ? "YYYYYY" : "ZZZZZZ"]);
        lobby.World.Codes.SetGenerator(generated.Dequeue);

        var next = await lobby.World.Lobbies.Create(Guid.NewGuid());

        next.Code.Should().NotBe(code);
        generated.Should().BeEmpty();
    }

    [Fact]
    public async Task MatchNobodyEntered_ClosesAfterJoinTimeout_CodeIsReleased_LobbyClosedLaterDoesNotReleaseAgain()
    {
        var lobby = await TestLobby.Create(guests: 0);
        var code = lobby.State.Code;

        lobby.RunCountdown();
        var match = lobby.World.Sessions[lobby.Game.SessionId];

        // the only user is stuck in the lobby and never enters the match
        lobby.World.Time.Advance(HostingTestWorld.JoinTimeout);
        match.Queue.Drain();

        match.Lifetime.IsTerminated.Should().BeTrue();
        (await lobby.World.Codes.Find(code)).Should().BeNull();

        // the code already belongs to another lobby when the old one is closed at last
        lobby.World.Codes.SetGenerator(() => code);
        var next = await lobby.World.Lobbies.Create(Guid.NewGuid());

        lobby.World.Disconnect(lobby.Session, lobby.Users[0]);

        lobby.Session.Lifetime.IsTerminated.Should().BeTrue();
        (await lobby.World.Codes.Find(code))!.SessionId.Should().Be(next.SessionId);
    }

    [Fact]
    public async Task MatchClosesBeforeTheRegistryAnsweredTheMark_CodeIsReleasedAfterTheMark()
    {
        var lobby = await TestLobby.Create(guests: 0);
        var code = lobby.State.Code;
        var gate = new TaskCompletionSource();

        lobby.World.Codes.HoldMarkStarted(gate);
        lobby.RunCountdown();

        var match = lobby.World.Sessions[lobby.Game.SessionId];

        // the registry has not been told yet: the lobby itself answers for its code
        (await lobby.World.Codes.Find(code))!.MatchSessionId.Should().Be(Guid.Empty);
        lobby.World.Lobbies.JoinLobby(lobby.Session.Session.Id, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Started);

        var player = lobby.World.Enter(match.Session.Id, lobby.Users[0].BackendId)!;
        lobby.World.Disconnect(lobby.Session, lobby.Users[0]);
        lobby.World.Disconnect(match, player);

        match.Lifetime.IsTerminated.Should().BeTrue();
        (await lobby.World.Codes.Find(code)).Should().NotBeNull();

        gate.SetResult();

        // the release follows the mark on a continuation
        await TestWait.Until(async () => await lobby.World.Codes.Find(code) == null);
    }

    [Fact]
    public async Task RepeatedStartWrite_DoesNotRestartCountdown()
    {
        var lobby = await TestLobby.Create(guests: 0);

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(2));
        lobby.WriteStart(true);

        lobby.Countdown.SecondsLeft.Should().Be(3);

        lobby.Advance(TimeSpan.FromSeconds(3));
        lobby.Game.SessionId.Should().NotBe(Guid.Empty);
    }

    // A write dropped by the writer check leaves the value and its version alone (the version goes into GetResponse).
    [Fact]
    public async Task StartWrittenByGuest_IsIgnored_NoCountdown_ValueAndVersionStay()
    {
        var lobby = await TestLobby.Create(guests: 1);
        var version = lobby.Service.Properties[SharedLobbyService.StartPropertyId].Version;

        lobby.WriteStart(true, lobby.Users[1]);

        lobby.Start.IsStarting.Should().BeFalse();
        lobby.Countdown.IsActive.Should().BeFalse();
        lobby.Service.Properties[SharedLobbyService.StartPropertyId].Version.Should().Be(version);

        lobby.Advance(TimeSpan.FromSeconds(10));
        lobby.Game.SessionId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task CancelWrittenByGuest_IsIgnored_CountdownGoesOn()
    {
        var lobby = await TestLobby.Create(guests: 1);

        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(2));

        lobby.WriteStart(false, lobby.Users[1]);

        lobby.Start.IsStarting.Should().BeTrue();
        lobby.Countdown.IsActive.Should().BeTrue();
        lobby.Countdown.SecondsLeft.Should().Be(3);

        lobby.Advance(TimeSpan.FromSeconds(3));
        lobby.Game.SessionId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StartWriter_FollowsTheLeader()
    {
        var lobby = await TestLobby.Create(guests: 2);

        lobby.World.Disconnect(lobby.Session, lobby.Users[0]);

        // the third user is still a guest, the second one is the leader now
        lobby.WriteStart(true, lobby.Users[2]);
        lobby.Countdown.IsActive.Should().BeFalse();

        lobby.WriteStart(true, lobby.Users[1]);
        lobby.Countdown.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task BackendProperties_WrittenByTheLeaderClient_AreIgnored()
    {
        var lobby = await TestLobby.Create(guests: 1);
        var leader = lobby.Users[0];
        var code = lobby.State.Code;

        lobby.Write(leader, SharedLobbyService.LobbyPropertyId, new LobbyState { Code = "AAAAAA", OwnerIndex = 2 });
        lobby.Write(leader, SharedLobbyService.CountdownPropertyId, new LobbyCountdownState { IsActive = true, SecondsLeft = 1 });
        lobby.Write(leader, SharedLobbyService.GamePropertyId, new LobbyGameState { ServerUrl = "ws://evil", SessionId = Guid.NewGuid() });

        lobby.State.Code.Should().Be(code);
        lobby.State.OwnerIndex.Should().Be(leader.Index);
        lobby.Countdown.IsActive.Should().BeFalse();
        lobby.Game.SessionId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Overview_CarriesTheVersionOfEveryProperty()
    {
        var lobby = await TestLobby.Create(guests: 1);

        lobby.World.Disconnect(lobby.Session, lobby.Users[0]);
        lobby.WriteStart(true);
        lobby.Advance(TimeSpan.FromSeconds(1));

        var overview = lobby.Service.CreateOverview();

        foreach (var property in overview.Properties)
        {
            var source = lobby.Service.Properties[property.PropertyId];

            property.Version.Should().Be(source.Version);
            property.Value.Should().Equal(source.RawValue);
        }

        // owner 1, owner 2
        overview.Properties.Single(property => property.PropertyId == SharedLobbyService.LobbyPropertyId)
                .Version.Should().Be(2);

        // 5, 4
        overview.Properties.Single(property => property.PropertyId == SharedLobbyService.CountdownPropertyId)
                .Version.Should().Be(2);

        overview.Properties.Single(property => property.PropertyId == SharedLobbyService.GamePropertyId)
                .Version.Should().Be(0);
    }

    // Two CreateLobby in a row must not leave two live lobbies and two codes for 30 s.
    [Fact]
    public async Task Create_SecondLobbyOfTheSameUser_FirstIsClosedAtOnce_CodeIsReleased()
    {
        var world = new HostingTestWorld();
        var user = Guid.NewGuid();

        var first = await world.Lobbies.Create(user);
        var second = await world.Lobbies.Create(user);

        world.Sessions[first.SessionId].Queue.Drain();

        world.Sessions[first.SessionId].Lifetime.IsTerminated.Should().BeTrue();
        (await world.Codes.Find(first.Code)).Should().BeNull();
        world.Lobbies.JoinLobby(first.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.NotFound);

        world.Sessions[second.SessionId].Lifetime.IsTerminated.Should().BeFalse();
        (await world.Codes.Find(second.Code)).Should().NotBeNull();
        world.Enter(second.SessionId, user).Should().NotBeNull();
    }

    [Fact]
    public async Task Join_AnotherLobby_DropsThePlaceInThePreviousOne_OthersKeepTheirs()
    {
        var world = new HostingTestWorld();
        var guest = Guid.NewGuid();

        var first = await world.Lobbies.Create(Guid.NewGuid());
        var second = await world.Lobbies.Create(Guid.NewGuid());

        world.Lobbies.JoinLobby(first.SessionId, guest).Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinLobby(first.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinLobby(first.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinLobby(first.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Full);

        world.Lobbies.JoinLobby(second.SessionId, guest).Should().Be(LobbyJoinStatus.Ok);
        world.Sessions[first.SessionId].Queue.Drain();

        // the place of the guest is free again, the first lobby lives on the reservations of the others
        world.Sessions[first.SessionId].Lifetime.IsTerminated.Should().BeFalse();
        world.Lobbies.JoinLobby(first.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinLobby(first.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Full);
    }

    [Fact]
    public async Task Join_SameLobbyAgain_IsOk_NothingIsDropped()
    {
        var world = new HostingTestWorld();
        var creator = Guid.NewGuid();

        var created = await world.Lobbies.Create(creator);

        world.Lobbies.JoinLobby(created.SessionId, creator).Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinLobby(created.SessionId, creator).Should().Be(LobbyJoinStatus.Ok);
        world.Sessions[created.SessionId].Queue.Drain();

        world.Sessions[created.SessionId].Lifetime.IsTerminated.Should().BeFalse();
        (await world.Codes.Find(created.Code)).Should().NotBeNull();
        world.Enter(created.SessionId, creator).Should().NotBeNull();
    }

    [Fact]
    public async Task Create_ByUserConnectedToALobby_TheLobbyWithTheUserInItStays()
    {
        var world = new HostingTestWorld();
        var user = Guid.NewGuid();

        var first = await world.Lobbies.Create(user);
        world.Enter(first.SessionId, user).Should().NotBeNull();

        await world.Lobbies.Create(user);
        world.Sessions[first.SessionId].Queue.Drain();

        world.Sessions[first.SessionId].Lifetime.IsTerminated.Should().BeFalse();
        (await world.Codes.Find(first.Code)).Should().NotBeNull();
    }

    [Fact]
    public async Task Create_AfterThePreviousLobbyIsClosed_NothingIsDropped()
    {
        var world = new HostingTestWorld();
        var user = Guid.NewGuid();

        var first = await world.Lobbies.Create(user);
        var entered = world.Enter(first.SessionId, user)!;
        world.Disconnect(world.Sessions[first.SessionId], entered);

        var second = await world.Lobbies.Create(user);

        world.Sessions[second.SessionId].Lifetime.IsTerminated.Should().BeFalse();
        world.Enter(second.SessionId, user).Should().NotBeNull();
    }

    // A guest that comes anew by the code right before zero (its old connection is not dropped yet when the last
    // step runs) is one user of the match.
    [Fact]
    public async Task Start_GuestWithTwoConnectionsAtZero_IsOneUserOfTheMatch()
    {
        var lobby = await TestLobby.Create(guests: 1);
        var guest = lobby.Users[1].BackendId;

        lobby.WriteStart(true);
        lobby.Advance(LobbyService.CountdownStep * (LobbyCountdownState.Seconds - 1));
        lobby.Countdown.SecondsLeft.Should().Be(1);

        // The drop of the old connection is queued, the new connection right after it, the last step after both.
        lobby.World.Lobbies.JoinLobby(lobby.Session.Session.Id, guest).Should().Be(LobbyJoinStatus.Ok);
        lobby.Session.Session.TryAccept(guest).Should().BeTrue();
        lobby.Session.EnqueueJoin(guest);
        lobby.World.Time.Advance(LobbyService.CountdownStep);
        lobby.Session.Queue.Drain();

        lobby.Game.SessionId.Should().NotBe(Guid.Empty);
        lobby.Game.PlayersCount.Should().Be(2);
    }

    // No match at zero: the failure goes on to the queue, the lobby is back where it was before the start.
    [Fact]
    public async Task Zero_MatchCreationFails_LobbyIsBackBeforeTheStart_TheLeaderStartsAgain()
    {
        var lobby = await TestLobby.Create(guests: 1);

        ReachZeroWithoutAMatch(lobby);

        lobby.Countdown.IsActive.Should().BeFalse();
        lobby.Start.IsStarting.Should().BeFalse();
        lobby.Session.Session.IsSealed.Should().BeFalse();

        var joined = lobby.World.Lobbies.Join(lobby.Session.Session.Id, Guid.Empty, lobby.Code, Guid.NewGuid());
        joined.Status.Should().Be(LobbyJoinStatus.Ok);

        lobby.World.FailMatchCreation(null);
        lobby.RunCountdown();
    }

    // The next error of the old order: a cancel of the leader after the failure stopped a countdown already over.
    [Fact]
    public async Task Zero_MatchCreationFails_CancelOfTheLeader_ChangesNothing()
    {
        var lobby = await TestLobby.Create(guests: 1);

        ReachZeroWithoutAMatch(lobby);

        lobby.WriteStart(false);

        lobby.Countdown.IsActive.Should().BeFalse();
    }

    // The same through the change of the leader, which also goes on to write the new leader.
    [Fact]
    public async Task Zero_MatchCreationFails_LeaderLeaves_TheNextUserLeads()
    {
        var lobby = await TestLobby.Create(guests: 1);

        ReachZeroWithoutAMatch(lobby);

        lobby.World.Disconnect(lobby.Session, lobby.Users[0]);

        lobby.State.OwnerIndex.Should().Be(lobby.Users[1].Index);
    }

    // The step of a second that has passed is queued behind a cancel: it changes nothing.
    [Fact]
    public async Task CancelQueuedBeforeTheStep_StepIsDropped()
    {
        var lobby = await TestLobby.Create(guests: 1);
        lobby.WriteStart(true);

        lobby.EnqueueWrite(lobby.Session.Owner.Owner.Value!, SharedLobbyService.StartPropertyId, new LobbyStartState());
        lobby.World.Time.Advance(LobbyService.CountdownStep);
        lobby.Session.Queue.Drain();

        lobby.Countdown.IsActive.Should().BeFalse();
        lobby.Countdown.SecondsLeft.Should().Be(0);
    }

    // The countdown of a lobby that has ended waits on nothing: its step queues no action after the end.
    [Fact]
    public async Task LobbyEndsDuringCountdown_StepQueuesNothing()
    {
        var lobby = await TestLobby.Create(guests: 1);
        lobby.WriteStart(true);

        lobby.Session.Lifetime.Terminate();
        lobby.Session.Queue.Drain();

        lobby.World.Time.Advance(LobbyService.CountdownStep * 2);

        lobby.Session.Queue.Count.Should().Be(0);
    }

    private static void ReachZeroWithoutAMatch(TestLobby lobby)
    {
        lobby.World.FailMatchCreation(new InvalidOperationException("no match"));
        lobby.WriteStart(true);

        var advance = () => lobby.Advance(LobbyService.CountdownStep * LobbyCountdownState.Seconds);

        advance.Should().Throw<InvalidOperationException>().WithMessage("no match");
    }
}
