using Cluster.Configs;
using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// OpponentFlagReshuffle: moves flags from flagged cells to random unflagged cells
/// within the rhombus pattern. One-to-one swap: removes flag from first flagged,
/// adds flag to random unflagged. Continues until one list is exhausted.
/// </summary>
public class OpponentFlagReshuffleTests : PlayerCardTestsBase
{
    private static readonly IGameRandom GameRandom = Substitute.For<IGameRandom>();

    private CardUseResult Use(IBoard board, CardUsePayload.OpponentFlagReshuffle payload)
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        opponent.Board.Returns(board);
        var gameContext = MockGameContext(invoker, opponent);
        return new OpponentFlagReshuffle(MockConfigs(), gameContext, GameRandom).Use(invoker, payload);
    }

    private (CardUseResult, MoveSnapshot) UseCapture(IBoard board, CardUsePayload.OpponentFlagReshuffle payload)
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        opponent.Board.Returns(board);
        var gameContext = MockGameContext(invoker, opponent);
        return new OpponentFlagReshuffle(MockConfigs(), gameContext, GameRandom).UseCapture(invoker, payload);
    }

    [Fact]
    public void Use_MovesFlags()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t f t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var result = Use(board, new CardUsePayload.OpponentFlagReshuffle { Position = target });

        result.Result.HasError.Should().BeFalse();

        // The original flagged cell at (2,1) should no longer be flagged
        board.Cells[new Position(2, 1)]
             .Should()
             .BeAssignableTo<ITakenCell>()
             .Which.IsFlagged.Should()
             .BeFalse();

        // Exactly one flag should exist in the pattern area (moved to another cell)
        var takenCells = board.Cells.Values
                              .Where(c => c.Status == CellStatus.Taken)
                              .Cast<ITakenCell>()
                              .ToList();

        takenCells.Count(c => c.IsFlagged)
                  .Should()
                  .Be(1,
                      "one flag was moved, total count stays the same");
    }

    [Fact]
    public void Use_NoFlags_NoChange()
    {
        // No flagged cells — while loop doesn't execute, still succeeds
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var result = Use(board, new CardUsePayload.OpponentFlagReshuffle { Position = target });

        result.Result.HasError.Should().BeFalse();

        // No flags anywhere
        board.Cells.Values
             .Where(c => c.Status == CellStatus.Taken)
             .Cast<ITakenCell>()
             .Count(c => c.IsFlagged)
             .Should()
             .Be(0);
    }

    [Fact]
    public void Use_AllCellsFlagged_NoUnflaggedTargets()
    {
        // All Taken cells in range are flagged — notFlagged is empty, loop doesn't execute
        var (board, target) = BoardParser.Parse("""
                                                f f f f f
                                                f f f f f
                                                f f x f f
                                                f f f f f
                                                f f f f f
                                                """);

        var result = Use(board, new CardUsePayload.OpponentFlagReshuffle { Position = target });

        result.Result.HasError.Should().BeFalse();

        // Flags stay where they were
        board.Cells[new Position(1, 1)]
             .Should()
             .BeAssignableTo<ITakenCell>()
             .Which.IsFlagged.Should()
             .BeTrue();
    }

    [Fact]
    public void Use_MultipleFlags_AllRelocated()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t t t
                                                t t t f t t t
                                                t t f t t t t
                                                t t t x t t t
                                                t t t t t t t
                                                t t t t t t t
                                                t t t t t t t
                                                """);

        var result = Use(board, new CardUsePayload.OpponentFlagReshuffle { Position = target });

        result.Result.HasError.Should().BeFalse();

        // Original positions should be unflagged
        board.Cells[new Position(3, 1)]
             .Should()
             .BeAssignableTo<ITakenCell>()
             .Which.IsFlagged.Should()
             .BeFalse();

        board.Cells[new Position(2, 2)]
             .Should()
             .BeAssignableTo<ITakenCell>()
             .Which.IsFlagged.Should()
             .BeFalse();

        // Total flag count in range should be preserved
        var inRangeCells = board.Cells.Values
                                .Where(c => c.Status == CellStatus.Taken)
                                .Cast<ITakenCell>();

        inRangeCells.Count(c => c.IsFlagged)
                    .Should()
                    .Be(2,
                        "number of flags should be preserved after reshuffle");
    }

    [Fact]
    public void Use_EmptyBoard_Fails()
    {
        var emptyBoard = new TestBoardBuilder(0).Build();

        var result = Use(emptyBoard, new CardUsePayload.OpponentFlagReshuffle { Position = new Position(0, 0) });

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_AllFreeCells_Fails()
    {
        var (board, _) = BoardParser.Parse("""
                                           _ _ _ _ _
                                           _ _ _ _ _
                                           _ _ _ _ _
                                           _ _ _ _ _
                                           _ _ _ _ _
                                           """);

        var result = Use(board, new CardUsePayload.OpponentFlagReshuffle { Position = new Position(2, 2) });

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_ActionDataHasTargetPlayer()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t f t t
                                                t t t t t
                                                t t t t t
                                                """);

        var ownerId = board.OwnerId;

        var (_, moveSnapshot) = UseCapture(board, new CardUsePayload.OpponentFlagReshuffle { Position = new Position(2, 2) });

        var actionData = moveSnapshot.GetLastCardAction<CardActionSnapshot.OpponentFlagReshuffle>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(ownerId);
    }
}