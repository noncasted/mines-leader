using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// OpponentFlagErase: removes flags from Taken cells in a rhombus pattern.
/// Iterates all Taken cells in range, removes flag from any that are flagged.
/// Succeeds even if no flags found (returns Ok after selecting Taken cells).
/// </summary>
public class OpponentFlagEraseTests {
    [Fact]
    public void Use_RemovesFlagsInPattern() {
        var (board, target) = BoardParser.Parse("""
            t t t t t
            t t f t t
            t f x f t
            t t f t t
            t t t t t
            """);

        var result = new OpponentFlagErase(board, CardConfigs.OpponentFlagErase,
            new CardUsePayload.OpponentFlagErase { Position = target }).Use();

        result.Result.HasError.Should().BeFalse();

        // All flags in the rhombus pattern should be removed
        BoardParser.AssertBoard(board, """
            t t t t t
            t t m t t
            t m t m t
            t t m t t
            t t t t t
            """);
    }

    [Fact]
    public void Use_NoFlagsInRange_StillSucceeds() {
        // No flags, but Taken cells exist — method succeeds (no-op on flags)
        var (board, target) = BoardParser.Parse("""
            t t t t t
            t t m t t
            t t x t t
            t t t t t
            t t t t t
            """);

        var result = new OpponentFlagErase(board, CardConfigs.OpponentFlagErase,
            new CardUsePayload.OpponentFlagErase { Position = target }).Use();

        result.Result.HasError.Should().BeFalse();
    }

    [Fact]
    public void Use_OnlyFlaggedCellsAffected() {
        // Mix of flagged and unflagged — only flagged lose their flag
        var (board, target) = BoardParser.Parse("""
            t t t t t t t
            t t t f t t t
            t t m t m t t
            t f t x t f t
            t t m t m t t
            t t t f t t t
            t t t t t t t
            """);

        var result = new OpponentFlagErase(board, CardConfigs.OpponentFlagErase,
            new CardUsePayload.OpponentFlagErase { Position = target }).Use();

        result.Result.HasError.Should().BeFalse();

        // Unflagged mines stay as mines, flagged mines become unflagged mines
        // Cells at (3,1), (1,3), (5,3), (3,5) were flagged — now unflagged
        board.Cells[new Position(3, 1)].Should().BeAssignableTo<ITakenCell>()
            .Which.IsFlagged.Should().BeFalse();
        board.Cells[new Position(1, 3)].Should().BeAssignableTo<ITakenCell>()
            .Which.IsFlagged.Should().BeFalse();

        // Unflagged mines stay unflagged with mines
        board.Cells[new Position(2, 2)].Should().BeAssignableTo<ITakenCell>()
            .Which.HasMine.Should().BeTrue();
    }

    [Fact]
    public void Use_EmptyBoard_Fails() {
        var emptyBoard = new TestBoardBuilder(0).Build();

        var result = new OpponentFlagErase(emptyBoard, CardConfigs.OpponentFlagErase,
            new CardUsePayload.OpponentFlagErase { Position = new Position(0, 0) }).Use();

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_AllFreeCells_Fails() {
        // All Free — SelectTaken returns empty
        var (board, _) = BoardParser.Parse("""
            _ _ _ _ _
            _ _ _ _ _
            _ _ _ _ _
            _ _ _ _ _
            _ _ _ _ _
            """);

        var result = new OpponentFlagErase(board, CardConfigs.OpponentFlagErase,
            new CardUsePayload.OpponentFlagErase { Position = new Position(2, 2) }).Use();

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_ActionDataHasTargetPlayer() {
        var (board, target) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t f t t
            t t t t t
            t t t t t
            """);

        var ownerId = board.OwnerId;
        var result = new OpponentFlagErase(board, CardConfigs.OpponentFlagErase,
            new CardUsePayload.OpponentFlagErase { Position = new Position(2, 2) }).Use();

        var snapshot = result.ActionData as CardActionSnapshot.OpponentFlagErase;
        snapshot.Should().NotBeNull();
        snapshot!.TargetPlayer.Should().Be(ownerId);
    }

    [Fact]
    public void Use_RemovesFlagFromNonMineCells() {
        // 'g' = flagged cell without mine
        var (board, target) = BoardParser.Parse("""
            t t t t t
            t t g t t
            t g x g t
            t t g t t
            t t t t t
            """);

        var result = new OpponentFlagErase(board, CardConfigs.OpponentFlagErase,
            new CardUsePayload.OpponentFlagErase { Position = target }).Use();

        result.Result.HasError.Should().BeFalse();

        // All 'g' cells should now be regular Taken cells
        BoardParser.AssertBoard(board, """
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            t t t t t
            """);
    }
}
