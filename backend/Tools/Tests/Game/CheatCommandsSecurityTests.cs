using Cluster.State;
using FluentAssertions;
using Game.GamePlay;
using Game.GamePlay.Snapshots;
using Game.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared;
using Xunit;
using ErrorCode = Shared.ErrorCode;

namespace Tests.Game;

/// <summary>
/// Cheat commands run only with ICheatsAccess (game flag Cheats and a Development host). A refused cheat is answered
/// with ErrorResponse Forbidden, written to the game log of the session and changes nothing.
/// </summary>
public class CheatCommandsSecurityTests
{
    [Fact]
    public void ChangeMana_WithoutAccess_ForbiddenAndStateUnchanged()
    {
        var fixture = CreateFixture(allowed: false);

        var response = fixture.Runner.RunRequest(fixture.User, new GameCheatContexts.ChangeMana { Value = 5 }, 1);

        response.Should().BeOfType<ErrorResponse>().Which.Code.Should().Be(ErrorCode.Forbidden);
        fixture.Player.Mana.Current.Should().Be(StartMana);
        fixture.SessionLogger.Received(1).Log(Arg.Is<string>(m => m.Contains("[Cheat] Refused") && m.Contains("ChangeMana")));
        fixture.SnapshotSender.DidNotReceiveWithAnyArgs().Send(default!);
    }

    [Fact]
    public void EndMatch_WithoutAccess_ForbiddenAndMatchGoesOn()
    {
        var fixture = CreateFixture(allowed: false);

        var response = fixture.Runner.RunRequest(fixture.User, new GameCheatContexts.EndMatch { Winner = fixture.User.Id }, 1);

        response.Should().BeOfType<ErrorResponse>().Which.Code.Should().Be(ErrorCode.Forbidden);
        fixture.Opponent.Health.Current.Should().Be(StartHealth);
        fixture.Round.DidNotReceive().SkipTurn();
    }

    [Fact]
    public void ChangeMana_WithAccess_Executes()
    {
        var fixture = CreateFixture(allowed: true);

        var response = fixture.Runner.RunRequest(fixture.User, new GameCheatContexts.ChangeMana { Value = 2 }, 1);

        response.Should().BeOfType<EmptyResponse>().Which.HasError.Should().BeFalse();
        fixture.Player.Mana.Current.Should().Be(StartMana + 2);
        fixture.SessionLogger.DidNotReceive().Log(Arg.Is<string>(m => m.Contains("[Cheat] Refused")));
    }

    [Fact]
    public void EveryCheatContext_WithoutAccess_IsForbidden()
    {
        var fixture = CreateFixture(allowed: false);
        var cheatTypes = typeof(GameCheatContexts).GetNestedTypes()
            .Where(type => typeof(INetworkContext).IsAssignableFrom(type))
            .ToList();

        cheatTypes.Should().NotBeEmpty();

        foreach (var type in cheatTypes)
        {
            var request = (INetworkContext)Activator.CreateInstance(type)!;
            var response = fixture.Runner.RunRequest(fixture.User, request, 1);

            response.Should().BeOfType<ErrorResponse>($"{type.Name} must be a refused cheat")
                .Which.Code.Should().Be(ErrorCode.Forbidden, type.Name);
        }
    }

    [Fact]
    public void EveryRegisteredCheat_IsCheatCommand()
    {
        var fixture = CreateFixture(allowed: false);
        var cheatTypes = typeof(GameCheatContexts).GetNestedTypes()
            .Where(type => typeof(INetworkContext).IsAssignableFrom(type));

        foreach (var type in cheatTypes)
        {
            var command = fixture.Commands.Responsible[type];
            IsCheatCommand(command.GetType()).Should().BeTrue($"{command.GetType().Name} must derive from CheatCommand<T>");
        }
    }

    [Theory]
    [InlineData("Development", true, true)]
    [InlineData("Development", false, false)]
    [InlineData("Production", true, false)]
    [InlineData("Staging", true, false)]
    public void CheatsAccess_NeedsFlagAndDevelopment(string environment, bool flag, bool expected)
    {
        var access = CreateAccess(environment, flag);

        access.IsAllowed.Should().Be(expected);

        if (expected == false)
            access.RefusalReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CheatsAccess_ReadsFlagAtRuntime()
    {
        var host = Substitute.For<IHostEnvironment>();
        host.EnvironmentName.Returns(Environments.Development);
        var flags = Substitute.For<IGameFlags>();
        var access = new CheatsAccess(host, flags);

        flags.CheatsEnabled.Returns(false);
        access.IsAllowed.Should().BeFalse();

        flags.CheatsEnabled.Returns(true);
        access.IsAllowed.Should().BeTrue();
    }

    private const int StartMana = 4;
    private const int StartHealth = 3;

    private static bool IsCheatCommand(Type type)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current.IsGenericType == true && current.GetGenericTypeDefinition() == typeof(CheatCommand<>))
                return true;
        }

        return false;
    }

    private static CheatsAccess CreateAccess(string environment, bool flag)
    {
        var host = Substitute.For<IHostEnvironment>();
        host.EnvironmentName.Returns(environment);
        var flags = Substitute.For<IGameFlags>();
        flags.CheatsEnabled.Returns(flag);
        return new CheatsAccess(host, flags);
    }

    private static Fixture CreateFixture(bool allowed)
    {
        var player = CreatePlayer();
        var opponent = CreatePlayer();

        var users = new Dictionary<IUser, IPlayer>
        {
            { player.User, player },
            { opponent.User, opponent }
        };

        var context = Substitute.For<IGameContext>();
        context.Players.Returns(new List<IPlayer> { player, opponent });
        context.UserToPlayer.Returns(users);

        var round = Substitute.For<IGameRound>();
        var snapshotSender = Substitute.For<ISnapshotSender>();
        var sessionLogger = Substitute.For<ISessionLogger>();

        var utils = new GameCommandUtils(
            context, round, Substitute.For<IServiceProvider>(),
            snapshotSender, Substitute.For<ISnapshotDiffGuard>(),
            Substitute.For<ILogger<GameCommandUtils>>(), sessionLogger,
            new MatchStatsTracker());

        var access = Substitute.For<ICheatsAccess>();
        access.IsAllowed.Returns(allowed);
        access.RefusalReason.Returns(allowed ? string.Empty : "game flag Cheats is off");

        var services = new ServiceCollection();
        services.AddSingleton(utils);
        services.AddSingleton(access);
        services.AddCheatCommands();

        var provider = services.BuildServiceProvider();
        var commands = new CommandsCollection([], provider.GetServices<IResponseCommand>());
        var runner = new SessionCommandRunner(commands, Substitute.For<ISessionLog>());

        return new Fixture(runner, commands, player.User, player, opponent, round, snapshotSender, sessionLogger);
    }

    private static IPlayer CreatePlayer()
    {
        var user = Substitute.For<IUser>();
        user.Id.Returns(Guid.NewGuid());
        user.IsBot.Returns(false);

        var modifiers = new Modifiers();
        var health = new Health(modifiers);
        health.SetMax(new MoveSnapshot(), StartHealth);
        health.SetCurrent(new MoveSnapshot(), StartHealth);

        var mana = new Mana(modifiers);
        mana.SetMax(new MoveSnapshot(), 10);
        mana.SetCurrent(new MoveSnapshot(), StartMana);

        var moves = new Moves(modifiers);
        moves.SetMax(new MoveSnapshot(), 5);
        moves.Restore(new MoveSnapshot());

        var player = Substitute.For<IPlayer>();
        player.User.Returns(user);
        player.Health.Returns(health);
        player.Mana.Returns(mana);
        player.Moves.Returns(moves);
        player.Hand.Returns(new Hand());
        player.Modifiers.Returns(modifiers);
        return player;
    }

    private sealed record Fixture(
        SessionCommandRunner Runner,
        ICommandsCollection Commands,
        IUser User,
        IPlayer Player,
        IPlayer Opponent,
        IGameRound Round,
        ISnapshotSender SnapshotSender,
        ISessionLogger SessionLogger);
}
