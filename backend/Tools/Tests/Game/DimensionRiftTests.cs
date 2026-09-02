using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// DimensionRift: swaps a rhombus area (size 2 — a 2x2 block) between the invoker's
/// and the opponent's boards, carrying over mines, flags and opened cells.
/// </summary>
public class DimensionRiftTests : PlayerCardTestsBase
{
    private static (CardUseResult Result, MoveSnapshot Snapshot) Use(
        IBoard ownerBoard,
        IBoard opponentBoard,
        Position position)
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        invoker.Board.Returns(ownerBoard);
        opponent.Board.Returns(opponentBoard);
        var gameContext = MockGameContext(invoker, opponent);

        return new DimensionRift(MockConfigs(), gameContext)
            .UseCapture(invoker, new CardUsePayload.DimensionRift { Position = position });
    }

    [Fact]
    public void Use_SwapsAreaBetweenBoards()
    {
        var (ownerBoard, _) = BoardParser.Parse("""
                                                t t t t t
                                                t m t t t
                                                t g _ t t
                                                t t t t t
                                                t t t t t
                                                """);

        var (opponentBoard, target) = BoardParser.Parse("""
                                                        t t t t t
                                                        t t f t t
                                                        t _ x t t
                                                        t t t t t
                                                        t t t t t
                                                        """);

        var (result, _) = Use(ownerBoard, opponentBoard, target);

        result.Result.HasError.Should().BeFalse();

        BoardParser.AssertBoard(ownerBoard, """
                                            t t t t t
                                            t t f t t
                                            t _ t t t
                                            t t t t t
                                            t t t t t
                                            """);

        BoardParser.AssertBoard(opponentBoard, """
                                               t t t t t
                                               t m t t t
                                               t g _ t t
                                               t t t t t
                                               t t t t t
                                               """);
    }

    [Fact]
    public void Use_MovesMinesBetweenBoards()
    {
        var (ownerBoard, _) = BoardParser.Parse("""
                                                t t t t t
                                                t m m t t
                                                t m m t t
                                                t t t t t
                                                t t t t t
                                                """);

        var (opponentBoard, target) = BoardParser.Parse("""
                                                        t t t t t
                                                        t t t t t
                                                        t t x t t
                                                        t t t t t
                                                        t t t t t
                                                        """);

        var (result, _) = Use(ownerBoard, opponentBoard, target);

        result.Result.HasError.Should().BeFalse();
        ownerBoard.MinesScanner.Mines.Should().Be(0);
        opponentBoard.MinesScanner.Mines.Should().Be(4);
    }

    [Fact]
    public void Use_RecalculatesMinesAroundOnBothBoards()
    {
        var (ownerBoard, _) = BoardParser.Parse("""
                                                t t t t t
                                                t m m t t
                                                t m m t t
                                                _ t t t t
                                                t t t t t
                                                """);

        var (opponentBoard, target) = BoardParser.Parse("""
                                                        t t t t t
                                                        t t t t t
                                                        t t x t t
                                                        _ t t t t
                                                        t t t t t
                                                        """);

        ownerBoard.Cells[new Position(0, 3)].AsFree().MinesAround.Should().Be(1);
        opponentBoard.Cells[new Position(0, 3)].AsFree().MinesAround.Should().Be(0);

        Use(ownerBoard, opponentBoard, target);

        ownerBoard.Cells[new Position(0, 3)].AsFree().MinesAround.Should().Be(0);
        opponentBoard.Cells[new Position(0, 3)].AsFree().MinesAround.Should().Be(1);
    }

    [Fact]
    public void Use_RecordsBothBoardsInActionData()
    {
        var (ownerBoard, _) = BoardParser.Parse("""
                                                t t t t t
                                                t m t t t
                                                t g _ t t
                                                t t t t t
                                                t t t t t
                                                """);

        var (opponentBoard, target) = BoardParser.Parse("""
                                                        t t t t t
                                                        t t f t t
                                                        t _ x t t
                                                        t t t t t
                                                        t t t t t
                                                        """);

        var (_, snapshot) = Use(ownerBoard, opponentBoard, target);

        var data = snapshot.GetLastCardAction<CardActionSnapshot.DimensionRift>();

        data.Should().NotBeNull();
        data!.TargetPlayer.Should().Be(opponentBoard.OwnerId);
        data.OwnerPlayer.Should().Be(ownerBoard.OwnerId);
        data.TargetCells.Should().HaveCount(4);

        // Owner board received the opponent's flagged mine and opened cell.
        data.OwnerFlaggedCells.Should().BeEquivalentTo([new Position(2, 1)]);
        data.OwnerOpenedCells.Select(c => c.Position).Should().BeEquivalentTo([new Position(1, 2)]);
        data.OwnerTakenCells.Should().BeEquivalentTo([new Position(1, 1), new Position(2, 1), new Position(2, 2)]);

        // Opponent board received the owner's flag and opened cell.
        data.TargetFlaggedCells.Should().BeEquivalentTo([new Position(1, 2)]);
        data.TargetOpenedCells.Select(c => c.Position).Should().BeEquivalentTo([new Position(2, 2)]);
        data.TargetUnflaggedCells.Should().BeEquivalentTo([new Position(1, 1), new Position(2, 1)]);
    }

    [Fact]
    public void Use_KeepsCellsOutsidePatternUntouched()
    {
        var (ownerBoard, _) = BoardParser.Parse("""
                                                m m m m m
                                                m t t m m
                                                m t t m m
                                                m m m m m
                                                m m m m m
                                                """);

        var (opponentBoard, target) = BoardParser.Parse("""
                                                        t t t t t
                                                        t t t t t
                                                        t t x t t
                                                        t t t t t
                                                        t t t t t
                                                        """);

        var (result, _) = Use(ownerBoard, opponentBoard, target);

        result.Result.HasError.Should().BeFalse();

        BoardParser.AssertBoard(ownerBoard, """
                                            m m m m m
                                            m t t m m
                                            m t t m m
                                            m m m m m
                                            m m m m m
                                            """);
    }

    [Fact]
    public void Use_EmptyOwnerBoard_Fails()
    {
        var ownerBoard = new TestBoardBuilder(0).Build();

        var (opponentBoard, target) = BoardParser.Parse("""
                                                        t t t
                                                        t x t
                                                        t t t
                                                        """);

        var (result, _) = Use(ownerBoard, opponentBoard, target);

        result.Result.HasError.Should().BeTrue();
    }
}
