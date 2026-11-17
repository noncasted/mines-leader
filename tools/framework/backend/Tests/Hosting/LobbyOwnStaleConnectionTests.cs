using FluentAssertions;
using Game.Hosting;
using Game.Lobbies;
using Xunit;

namespace Framework.Tests.Hosting;

// A user alone in a lobby lost its client without a close and comes back before the keep-alive notices: its stale
// connection is all that holds the lobby, so the lobby is refused and closes once that connection is dropped (see
// SessionCloseTests.StaleConnectionAlone_ReserveAgain_IsRefused_DropsIt_ClosesSession). The answer is the one of a closed
// lobby, never Full.
public class LobbyOwnStaleConnectionTests
{
    [Fact]
    public async Task Join_LobbyHeldOnlyByOwnStaleConnection_ReturnsNotFound_LobbyCloses()
    {
        var world = new HostingTestWorld();
        var creator = Guid.NewGuid();
        var created = await world.Lobbies.Create(creator);
        var session = world.Sessions[created.SessionId];
        world.Enter(created.SessionId, creator).Should().NotBeNull();

        world.Lobbies.JoinLobby(created.SessionId, creator).Should().Be(LobbyJoinStatus.NotFound);

        session.Queue.Drain();

        session.Lifetime.IsTerminated.Should().BeTrue();
        world.Codes.Entries.Should().BeEmpty();
    }

    // The lobby of the late players held only by the stale connection of the player is closing: the player gets a new one.
    [Fact]
    public void JoinLate_LobbyOfLatePlayersHeldOnlyByOwnStaleConnection_NewLobby()
    {
        var world = new HostingTestWorld();
        var match = world.CreatePublicMatch();
        match.ReserveAndEnter();
        var late = Guid.NewGuid();

        var first = world.Lobbies.JoinLate(match.Session.Id, late);
        first.Status.Should().Be(LobbyJoinStatus.Ok);
        world.Enter(first.SessionId, late).Should().NotBeNull();

        var second = world.Lobbies.JoinLate(match.Session.Id, late);

        second.Status.Should().Be(LobbyJoinStatus.Ok);
        second.SessionId.Should().NotBe(first.SessionId);

        world.DrainAll();

        world.Sessions[first.SessionId].Lifetime.IsTerminated.Should().BeTrue();
        world.Sessions[second.SessionId].Lifetime.IsTerminated.Should().BeFalse();
        world.Enter(second.SessionId, late).Should().NotBeNull();
    }
}
