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

/// <summary>
/// Аккорд списывает ход только когда реально что-то открывает. Пустой аккорд
/// (все соседи открыты или под флагами) и аккорд с несовпавшими флагами — отказ без хода.
/// </summary>
public class OpenMultipleCellsCommandTests
{
    [Fact]
    public void AllNeighboursOpenOrFlagged_FailsWithoutMove()
    {
        var (board, _) = BoardParser.Parse("""
                                                t t t t t
                                                t f _ _ t
                                                t _ _ _ t
                                                t _ _ _ t
                                                t t t t t
                                                """);
        var player = CreatePlayer(board);

        var response = Execute(board, player, new Position(2, 2));

        response.HasError.Should().BeTrue();
        response.Message.Should().Contain("reveals nothing");
        player.Moves.DidNotReceive().OnUsed(Arg.Any<MoveSnapshot>(), Arg.Any<int>());
    }

    [Fact]
    public void FlagCountMismatch_FailsWithoutMove()
    {
        var (board, _) = BoardParser.Parse("""
                                                t t t t t
                                                t m t t t
                                                t t _ t t
                                                t t t t t
                                                t t t t t
                                                """);
        var player = CreatePlayer(board);

        var response = Execute(board, player, new Position(2, 2));

        response.HasError.Should().BeTrue();
        response.Message.Should().Contain("1 flag(s) around, 0 placed");
        player.Moves.DidNotReceive().OnUsed(Arg.Any<MoveSnapshot>(), Arg.Any<int>());
        board.Cells[new Position(1, 2)].Status.Should().Be(CellStatus.Taken);
    }

    [Fact]
    public void UnflaggedClosedNeighbour_OpensAndUsesMove()
    {
        var (board, _) = BoardParser.Parse("""
                                                t t t t t
                                                t f t t t
                                                t t _ t t
                                                t t t t t
                                                t t t t t
                                                """);
        var player = CreatePlayer(board);

        var response = Execute(board, player, new Position(2, 2));

        response.HasError.Should().BeFalse();
        player.Moves.Received(1).OnUsed(Arg.Any<MoveSnapshot>(), 1);
        board.Cells[new Position(3, 3)].Status.Should().Be(CellStatus.Free);
    }

    [Fact]
    public void NoMovesLeft_FailsBeforeTouchingBoard()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t f t t t
                                           t t _ t t
                                           t t t t t
                                           t t t t t
                                           """);
        var player = CreatePlayer(board);
        player.Moves.Left.Returns(0);

        var response = Execute(board, player, new Position(2, 2));

        response.HasError.Should().BeTrue();
        response.Message.Should().Be("No moves left");
        board.Cells[new Position(3, 3)].Status.Should().Be(CellStatus.Taken);
    }

    private static EmptyResponse Execute(IBoard board, IPlayer player, Position target)
    {
        var utils = new GameCommandUtils(
            Substitute.For<IGameContext>(), RoundOf(player), Substitute.For<IServiceProvider>(),
            Substitute.For<ISnapshotSender>(), Substitute.For<ISnapshotDiffGuard>(),
            Substitute.For<ILogger<GameCommandUtils>>(), Substitute.For<ISessionLogger>(),
            new MatchStatsTracker());

        var command = new TestableCommand(utils);
        var context = new GameCommand<SharedGameAction.OpenMultiple>.Context
        {
            Player = player,
            Lifetime = new Lifetime(),
            Snapshot = new MoveSnapshot()
        };

        return command.Execute(context, new SharedGameAction.OpenMultiple { Position = target });
    }

    private static IGameRound RoundOf(IPlayer player)
    {
        var round = Substitute.For<IGameRound>();
        round.CurrentPlayer.Value.Returns(player);
        return round;
    }

    private static IPlayer CreatePlayer(IBoard board)
    {
        var player = Substitute.For<IPlayer>();
        player.Board.Returns(board);
        player.Moves.Returns(Substitute.For<IMoves>());
        player.Moves.Left.Returns(5);
        player.Health.Returns(Substitute.For<IHealth>());
        player.Modifiers.Returns(Substitute.For<IModifiers>());
        return player;
    }

    private class TestableCommand : OpenMultipleCellsCommand
    {
        public TestableCommand(GameCommandUtils utils) : base(utils) { }

        public new EmptyResponse Execute(GameCommand<SharedGameAction.OpenMultiple>.Context context, SharedGameAction.OpenMultiple request)
            => base.Execute(context, request);
    }
}
