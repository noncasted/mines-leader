using Cluster.Lobbies;
using FluentAssertions;
using Game.Lobbies;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Hosting;

// The lobby factory of the game fails and the release of the code fails after it: the caller gets the cause.
public class LobbyCreateFailureTests
{
    [Fact]
    public async Task FactoryFails_ReleaseOfTheCodeFailsToo_TheFailureOfTheFactoryIsThrown()
    {
        var world = new HostingTestWorld();
        world.FailLobbyCreation(new InvalidOperationException("The game has failed to build the lobby"));

        var codes = Substitute.For<ILobbyCodes>();
        codes.Register(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns("AAAAAA");
        codes.Release(Arg.Any<string>(), Arg.Any<Guid>()).Returns(Task.FromException(new TimeoutException("The registry is not reachable")));

        var lobbies = new LobbySessions(
            world,
            world.Collection,
            codes,
            world.Leases,
            world.EntryPolicy,
            world.Options,
            world.Discovery,
            NullLogger<LobbySessions>.Instance);

        var create = () => lobbies.Create(Guid.NewGuid());

        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("The game has failed to build the lobby");
        await codes.Received(1).Release("AAAAAA", Arg.Any<Guid>());
    }
}
