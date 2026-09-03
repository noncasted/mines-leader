using Common.Reactive;
using FluentAssertions;
using Game.GamePlay;
using Game.GamePlay.Snapshots;
using Game.Session;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class OpenCellCommandTests
{
    [Fact]
    public void Execute_HitsMineWithShield_CallsRemoveOne()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        ((ITakenCell)board.Cells[target]).SetMine();

        var player = Substitute.For<IPlayer>();
        player.Board.Returns(board);
        player.Modifiers.Returns(Substitute.For<IModifiers>());
        player.Modifiers.Get(PlayerModifier.Shield).Returns(1f);
        player.Health.Returns(Substitute.For<IHealth>());
        player.Moves.Left.Returns(5);

        var ctx = Substitute.For<IGameContext>();
        var utils = new GameCommandUtils(
            ctx, RoundOf(player), Substitute.For<IServiceProvider>(),
            Substitute.For<ISnapshotSender>(), Substitute.For<ISnapshotDiffGuard>(),
            Substitute.For<ILogger<GameCommandUtils>>(), Substitute.For<ISessionLogger>(),
            new MatchStatsTracker());

        var cmd = new TestableOpenCellCommand(utils);
        var snapshot = new MoveSnapshot();
        var context = new GameCommand<SharedGameAction.Open>.Context
        {
            Player = player,
            Lifetime = new Lifetime(),
            Snapshot = snapshot
        };

        cmd.Execute(context, new SharedGameAction.Open { Position = target });

        player.Modifiers.Received(1).RemoveOne(Arg.Is<MoveSnapshot>(s => s == snapshot), PlayerModifier.Shield);
        player.Health.DidNotReceive().TakeDamage(Arg.Any<MoveSnapshot>(), Arg.Any<int>());
    }

    [Fact]
    public void Execute_HitsMineWithoutShield_TakesDamage()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        ((ITakenCell)board.Cells[target]).SetMine();

        var player = Substitute.For<IPlayer>();
        player.Board.Returns(board);
        player.Modifiers.Returns(Substitute.For<IModifiers>());
        player.Modifiers.Get(PlayerModifier.Shield).Returns(0f);
        player.Health.Returns(Substitute.For<IHealth>());
        player.Moves.Left.Returns(5);

        var ctx = Substitute.For<IGameContext>();
        var utils = new GameCommandUtils(
            ctx, RoundOf(player), Substitute.For<IServiceProvider>(),
            Substitute.For<ISnapshotSender>(), Substitute.For<ISnapshotDiffGuard>(),
            Substitute.For<ILogger<GameCommandUtils>>(), Substitute.For<ISessionLogger>(),
            new MatchStatsTracker());

        var cmd = new TestableOpenCellCommand(utils);
        var snapshot = new MoveSnapshot();
        var context = new GameCommand<SharedGameAction.Open>.Context
        {
            Player = player,
            Lifetime = new Lifetime(),
            Snapshot = snapshot
        };

        cmd.Execute(context, new SharedGameAction.Open { Position = target });

        player.Modifiers.DidNotReceive().RemoveOne(Arg.Any<MoveSnapshot>(), Arg.Any<PlayerModifier>());
        player.Health.Received(1).TakeDamage(Arg.Is<MoveSnapshot>(s => s == snapshot), 1);
    }

    [Fact]
    public void Execute_NoMovesLeft_FailsBeforeTouchingBoard()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t
                                                t x t
                                                t t t
                                                """);
        var player = Substitute.For<IPlayer>();
        player.Board.Returns(board);
        player.Moves.Left.Returns(0);

        var response = Run(player, RoundOf(player), target);

        response.HasError.Should().BeTrue();
        response.Message.Should().Be("No moves left");
        board.Cells[target].Status.Should().Be(CellStatus.Taken);
        player.Moves.DidNotReceive().OnUsed(Arg.Any<MoveSnapshot>(), Arg.Any<int>());
    }

    [Fact]
    public void Execute_OffTurn_FailsNotYourTurn()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t
                                                t x t
                                                t t t
                                                """);
        var player = Substitute.For<IPlayer>();
        player.Board.Returns(board);
        player.Moves.Left.Returns(5);

        var response = Run(player, RoundOf(Substitute.For<IPlayer>()), target);

        response.HasError.Should().BeTrue();
        response.Message.Should().Be("Not your turn");
        board.Cells[target].Status.Should().Be(CellStatus.Taken);
    }

    private static EmptyResponse Run(IPlayer player, IGameRound round, Position target)
    {
        var utils = new GameCommandUtils(
            Substitute.For<IGameContext>(), round, Substitute.For<IServiceProvider>(),
            Substitute.For<ISnapshotSender>(), Substitute.For<ISnapshotDiffGuard>(),
            Substitute.For<ILogger<GameCommandUtils>>(), Substitute.For<ISessionLogger>(),
            new MatchStatsTracker());
        var context = new GameCommand<SharedGameAction.Open>.Context
        {
            Player = player,
            Lifetime = new Lifetime(),
            Snapshot = new MoveSnapshot()
        };
        return new TestableOpenCellCommand(utils).Execute(context, new SharedGameAction.Open { Position = target });
    }

    private static IGameRound RoundOf(IPlayer player)
    {
        var round = Substitute.For<IGameRound>();
        round.CurrentPlayer.Value.Returns(player);
        return round;
    }

    private class TestableOpenCellCommand : OpenCellCommand
    {
        public TestableOpenCellCommand(GameCommandUtils utils) : base(utils) { }
        public new EmptyResponse Execute(GameCommand<SharedGameAction.Open>.Context context, SharedGameAction.Open request) => base.Execute(context, request);
    }
}
