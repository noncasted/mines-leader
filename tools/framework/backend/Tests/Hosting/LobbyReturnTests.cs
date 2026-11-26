using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Session;
using Game.Lobbies;
using Game.Session;
using Shared;
using Xunit;

namespace Framework.Tests.Hosting;

// LobbyOptions.ReturnsAfterMatch: the code of a lobby comes back from its match to a new lobby of the game (LobbyCreation.Return)
// once per match - at the first player that leaves the match or at its close - with the places of the players of the
// match reserved and the leader of the match leading it. Off: the code is released with the match.
public class LobbyReturnTests
{
    private const string Code = "K7Q2MX";

    [Fact]
    public async Task Return_FirstUserLeavesMatch_CreatesLobbyWithTheSameCode()
    {
        var started = await StartMatch(returnsAfterMatch: true);
        var world = started.Lobby.World;

        world.Disconnect(started.Match, started.Players[0]);

        var creation = ReturnCreation(world);

        creation.Code.Should().Be(Code);
        creation.MaxUsers.Should().Be(world.Options.MaxUsers);
        creation.Return!.LobbySessionId.Should().Be(started.Lobby.Session.Session.Id);
        creation.Return.MatchSessionId.Should().Be(started.Match.Session.Id);
        creation.Return.LeaderId.Should().Be(started.Lobby.Users[0].BackendId);
        creation.Return.LeaderId.Should().Be(world.MatchLeaderId);

        // the match goes on for the others
        started.Match.Lifetime.IsTerminated.Should().BeFalse();

        await TestWait.Until(() => world.Codes.Calls.Contains($"Return {Code}"));

        var entry = await world.Codes.Find(Code);

        entry!.SessionId.Should().Be(creation.Id);
        entry.MatchSessionId.Should().Be(Guid.Empty);

        var calls = world.Codes.Calls.ToList();

        calls.IndexOf($"Mark {Code}").Should().BeLessThan(calls.IndexOf($"Return {Code}"));
        calls.Should().NotContain($"Release {Code}");
    }

    [Fact]
    public async Task Return_SecondLeave_NoSecondLobby()
    {
        var started = await StartMatch(returnsAfterMatch: true);
        var world = started.Lobby.World;

        world.Disconnect(started.Match, started.Players[0]);
        var creation = ReturnCreation(world);

        world.Disconnect(started.Match, started.Players[2]);
        world.Disconnect(started.Match, started.Players[1]);

        started.Match.Lifetime.IsTerminated.Should().BeTrue();
        ReturnCreation(world).Id.Should().Be(creation.Id);

        await TestWait.Until(() => world.Codes.Calls.Contains($"Return {Code}"));

        world.Codes.Calls.Should().NotContain($"Release {Code}");
        (await world.Codes.Find(Code))!.SessionId.Should().Be(creation.Id);
    }

    [Fact]
    public async Task Return_MatchClosedWithNobodyLeft_StillReturns()
    {
        var world = CreateWorld(returnsAfterMatch: true);
        var lobby = await TestLobby.Create(world, guests: 2);

        lobby.RunCountdown();
        var match = world.Sessions[lobby.Game.SessionId];

        foreach (var user in lobby.Users)
            world.Disconnect(lobby.Session, user);

        // nobody has made it into the match: it is closed after the time of its reservations
        world.Time.Advance(HostingTestWorld.JoinTimeout);
        match.Queue.Drain();

        match.Lifetime.IsTerminated.Should().BeTrue();

        var creation = ReturnCreation(world);

        creation.Return!.MatchSessionId.Should().Be(match.Session.Id);

        await TestWait.Until(() => world.Codes.Calls.Contains($"Return {Code}"));

        (await world.Codes.Find(Code))!.SessionId.Should().Be(creation.Id);
        world.Codes.Calls.Should().NotContain($"Release {Code}");
    }

    [Fact]
    public async Task Return_ReservesThePlayersOfTheMatch()
    {
        var started = await StartMatch(returnsAfterMatch: true);
        var world = started.Lobby.World;

        world.Disconnect(started.Match, started.Players[1]);

        var returned = world.Sessions[ReturnCreation(world).Id];
        var userIds = started.Lobby.Users.Select(user => user.BackendId).ToList();

        returned.Admission.GetPendingUsers(world.Time.GetUtcNow().UtcDateTime).Should().BeEquivalentTo(userIds);

        // a player of the match finds its place kept
        world.Lobbies.Join(returned.Session.Id, Guid.Empty, Code, userIds[2]).Status.Should().Be(LobbyJoinStatus.Ok);

        // one free place of the four is left for somebody else
        world.Lobbies.Join(returned.Session.Id, Guid.Empty, Code, Guid.NewGuid()).Status.Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.Join(returned.Session.Id, Guid.Empty, Code, Guid.NewGuid()).Status.Should().Be(LobbyJoinStatus.Full);
    }

    [Fact]
    public async Task Return_Lobby_ClosedEmpty_ReleasesTheCode()
    {
        var started = await StartMatch(returnsAfterMatch: true);
        var world = started.Lobby.World;

        world.Disconnect(started.Match, started.Players[0]);

        var returned = world.Sessions[ReturnCreation(world).Id];

        await TestWait.Until(() => world.Codes.Calls.Contains($"Return {Code}"));

        // nobody comes back in time
        world.Time.Advance(HostingTestWorld.JoinTimeout);
        returned.Queue.Drain();

        returned.Lifetime.IsTerminated.Should().BeTrue();

        // the release is accepted only from the session that holds the code: the returned lobby
        await TestWait.Until(async () => await world.Codes.Find(Code) == null);

        world.Codes.Calls.Count(call => call == $"Release {Code}").Should().Be(1);
        world.Codes.Calls.Last().Should().Be($"Release {Code}");

        // the rest of the match closes later and releases nothing
        world.Disconnect(started.Match, started.Players[2]);
        world.Disconnect(started.Match, started.Players[1]);

        started.Match.Lifetime.IsTerminated.Should().BeTrue();
        world.Codes.Calls.Count(call => call == $"Release {Code}").Should().Be(1);
    }

    [Fact]
    public async Task Return_Leader_TakesTheLeadOnEntry()
    {
        var started = await StartMatch(returnsAfterMatch: true);
        var world = started.Lobby.World;
        var leaderId = started.Lobby.Users[0].BackendId;
        var thirdId = started.Lobby.Users[2].BackendId;

        world.Disconnect(started.Match, started.Players[2]);

        var returned = world.Sessions[ReturnCreation(world).Id];
        var service = returned.LobbyService();

        // the third player of the match comes back first: it leads until the leader of the match is in
        world.Lobbies.Join(returned.Session.Id, Guid.Empty, Code, thirdId).Status.Should().Be(LobbyJoinStatus.Ok);
        var third = world.Enter(returned.Session.Id, thirdId)!;

        service.Read<LobbyState>(SharedLobbyService.LobbyPropertyId).OwnerIndex.Should().Be(third.Index);

        world.Lobbies.Join(returned.Session.Id, Guid.Empty, Code, leaderId).Status.Should().Be(LobbyJoinStatus.Ok);
        var leader = world.Enter(returned.Session.Id, leaderId)!;

        leader.Index.Should().NotBe(third.Index);
        service.Read<LobbyState>(SharedLobbyService.LobbyPropertyId).OwnerIndex.Should().Be(leader.Index);

        // the leader leaves: the lead goes back to the next user
        world.Disconnect(returned, leader);

        service.Read<LobbyState>(SharedLobbyService.LobbyPropertyId).OwnerIndex.Should().Be(third.Index);
    }

    [Fact]
    public async Task Return_FactoryThrows_ReleasesTheCode()
    {
        var started = await StartMatch(returnsAfterMatch: true);
        var world = started.Lobby.World;

        world.FailLobbyCreation(new InvalidOperationException("The game has no lobby to return to"));

        // the failure stays with the return: the match goes on
        world.Disconnect(started.Match, started.Players[0]);

        started.Match.Lifetime.IsTerminated.Should().BeFalse();

        await TestWait.Until(async () => await world.Codes.Find(Code) == null);

        var calls = world.Codes.Calls.ToList();

        calls.Should().NotContain($"Return {Code}");
        calls.IndexOf($"Mark {Code}").Should().BeLessThan(calls.IndexOf($"Release {Code}"));

        // the rest of the match closes later and releases nothing
        world.Disconnect(started.Match, started.Players[1]);
        world.Disconnect(started.Match, started.Players[2]);

        world.Codes.Calls.Count(call => call == $"Release {Code}").Should().Be(1);
    }

    [Fact]
    public async Task NoReturn_FlagOff_ReleasesWithTheMatch()
    {
        var started = await StartMatch(returnsAfterMatch: false);
        var world = started.Lobby.World;

        world.Disconnect(started.Match, started.Players[0]);

        // one player has left: the match goes on and keeps the code
        (await world.Codes.Find(Code))!.MatchSessionId.Should().Be(started.Match.Session.Id);

        world.Disconnect(started.Match, started.Players[2]);
        world.Disconnect(started.Match, started.Players[1]);

        started.Match.Lifetime.IsTerminated.Should().BeTrue();

        await TestWait.Until(async () => await world.Codes.Find(Code) == null);

        world.LobbyCreations.Should().OnlyContain(creation => creation.Return == null);
        world.Codes.Calls.Should().NotContain($"Return {Code}");
        world.Codes.Calls.Count(call => call == $"Release {Code}").Should().Be(1);
    }

    private static HostingTestWorld CreateWorld(bool returnsAfterMatch)
    {
        var world = new HostingTestWorld(maxUsers: 4, returnsAfterMatch: returnsAfterMatch);
        world.Codes.SetGenerator(() => Code);

        return world;
    }

    // A lobby of three: the leader and two guests start a match, enter it and leave the lobby.
    private static async Task<StartedMatch> StartMatch(bool returnsAfterMatch)
    {
        var world = CreateWorld(returnsAfterMatch);
        var lobby = await TestLobby.Create(world, guests: 2);

        lobby.RunCountdown();

        var match = world.Sessions[lobby.Game.SessionId];
        var players = lobby.Users.Select(user => world.Enter(match.Session.Id, user.BackendId)!).ToList();

        foreach (var user in lobby.Users)
            world.Disconnect(lobby.Session, user);

        lobby.Session.Lifetime.IsTerminated.Should().BeTrue();

        return new StartedMatch(lobby, match, players);
    }

    private static LobbyCreation ReturnCreation(HostingTestWorld world)
    {
        return world.LobbyCreations.Where(creation => creation.Return != null).Should().ContainSingle().Subject;
    }

    private record StartedMatch(TestLobby Lobby, TestSession Match, IReadOnlyList<IUser> Players);
}
