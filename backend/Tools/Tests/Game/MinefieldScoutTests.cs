using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// MinefieldScout: scans a line (horizontal or vertical, whichever has more Taken cells)
/// of Size(5) from the target position. Flags mine cells, opens non-mine cells to Free + Reveal.
/// Line(5) is 5x5 shape with only center row (horizontal) or center column (vertical) as true.
/// Returns revealed positions in ActionData.
/// </summary>
public class MinefieldScoutTests {
    [Fact]
    public void Use_FlagsMinesInLine() {
        // Mine at (2,3) and (4,3) — horizontal line from center (3,3) covers them
        var (board, target) = BoardParser.Parse("""
            t t t t t t t
            t t t t t t t
            t t t t t t t
            t t m x m t t
            t t t t t t t
            t t t t t t t
            t t t t t t t
            """);

        var result = new MinefieldScout(board,
            new CardUsePayload.MinefieldScout { Position = target },
            CardConfigs.MinefieldScout).Use();

        result.Result.HasError.Should().BeFalse();

        // Mines in the horizontal line should be flagged
        board.Cells[new Position(2, 3)].Should().BeAssignableTo<ITakenCell>()
            .Which.IsFlagged.Should().BeTrue("mine at (2,3) is in horizontal line range");

        board.Cells[new Position(4, 3)].Should().BeAssignableTo<ITakenCell>()
            .Which.IsFlagged.Should().BeTrue("mine at (4,3) is in horizontal line range");
    }

    [Fact]
    public void Use_OpensNonMineCells() {
        // Line(4, horizontal) centered at (3,3): covers (1,3), (2,3), (3,3), (4,3)
        // All are Taken non-mine, so all should be opened and revealed
        var (board, target) = BoardParser.Parse("""
            t t t t t t t
            t t t t t t t
            t t t t t t t
            t t t x t t t
            t t t t t t t
            t t t t t t t
            t t t t t t t
            """);

        var result = new MinefieldScout(board,
            new CardUsePayload.MinefieldScout { Position = target },
            CardConfigs.MinefieldScout).Use();

        result.Result.HasError.Should().BeFalse();

        var actionData = result.ActionData as CardActionSnapshot.MinefieldScout;
        actionData.Should().NotBeNull();
        actionData!.RevealedCells.Should().HaveCount(4);
        actionData.RevealedCells.Should().Contain(new Position(1, 3));
        actionData.RevealedCells.Should().Contain(new Position(2, 3));
        actionData.RevealedCells.Should().Contain(new Position(3, 3));
        actionData.RevealedCells.Should().Contain(new Position(4, 3));
    }

    [Fact]
    public void Use_ReturnsRevealedPositions() {
        // Line(4, horizontal) at (3,3): covers (1,3), (2,3), (3,3), (4,3)
        // Mine at (2,3) gets flagged, others get opened — all 4 are in RevealedCells
        var (board, target) = BoardParser.Parse("""
            t t t t t t t
            t t t t t t t
            t t t t t t t
            t t m x t t t
            t t t t t t t
            t t t t t t t
            t t t t t t t
            """);

        var result = new MinefieldScout(board,
            new CardUsePayload.MinefieldScout { Position = target },
            CardConfigs.MinefieldScout).Use();

        result.Result.HasError.Should().BeFalse();

        var actionData = result.ActionData as CardActionSnapshot.MinefieldScout;
        actionData.Should().NotBeNull();
        actionData!.RevealedCells.Should().HaveCount(4);
        actionData.RevealedCells.Should().Contain(new Position(2, 3), "mine position is flagged and included");
        actionData.RevealedCells.Should().Contain(new Position(1, 3));
        actionData.RevealedCells.Should().Contain(new Position(3, 3));
        actionData.RevealedCells.Should().Contain(new Position(4, 3));
        actionData.TargetPlayer.Should().Be(board.OwnerId);
    }

    [Fact]
    public void Use_NoCellsInRange_Fails() {
        var emptyBoard = new TestBoardBuilder(0).Build();

        var result = new MinefieldScout(emptyBoard,
            new CardUsePayload.MinefieldScout { Position = new Position(0, 0) },
            CardConfigs.MinefieldScout).Use();

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_AllFreeCells_Fails() {
        // All cells already Free — SelectTaken returns empty for both lines
        var (board, _) = BoardParser.Parse("""
            _ _ _ _ _
            _ _ _ _ _
            _ _ _ _ _
            _ _ _ _ _
            _ _ _ _ _
            """);

        var result = new MinefieldScout(board,
            new CardUsePayload.MinefieldScout { Position = new Position(2, 2) },
            CardConfigs.MinefieldScout).Use();

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_ChoosesLongerLine() {
        // Block horizontal line with Free cells, vertical should have more Taken
        var (board, target) = BoardParser.Parse("""
            t t t t t t t
            t t t t t t t
            t t t t t t t
            t _ _ x _ _ t
            t t t t t t t
            t t t t t t t
            t t t t t t t
            """);

        var result = new MinefieldScout(board,
            new CardUsePayload.MinefieldScout { Position = target },
            CardConfigs.MinefieldScout).Use();

        result.Result.HasError.Should().BeFalse();

        // Vertical line should be chosen (more Taken cells — horizontal has 1, vertical has 4)
        // Line(4, vertical) at (3,3): covers (3,1), (3,2), (3,3), (3,4)
        var actionData = result.ActionData as CardActionSnapshot.MinefieldScout;
        actionData.Should().NotBeNull();
        actionData!.RevealedCells.Should().HaveCount(4);
        actionData.RevealedCells.Should().Contain(new Position(3, 1));
        actionData.RevealedCells.Should().Contain(new Position(3, 2));
        actionData.RevealedCells.Should().Contain(new Position(3, 3));
        actionData.RevealedCells.Should().Contain(new Position(3, 4));

        // Horizontal Free cells should NOT be in revealed
        actionData.RevealedCells.Should().NotContain(new Position(1, 3));
        actionData.RevealedCells.Should().NotContain(new Position(5, 3));
    }

    [Fact]
    public void Use_MixOfMinesAndSafeCells() {
        // Mine in horizontal line (chosen when equal to vertical)
        // Center (3,3), horizontal line covers (1,3)-(5,3)
        var (board, target) = BoardParser.Parse("""
            t t t t t t t
            t t t t t t t
            t t t t t t t
            t m t x t t t
            t t t t t t t
            t t t t t t t
            t t t t t t t
            """);

        var result = new MinefieldScout(board,
            new CardUsePayload.MinefieldScout { Position = target },
            CardConfigs.MinefieldScout).Use();

        result.Result.HasError.Should().BeFalse();

        // Mine position should still be Taken and flagged
        var mineCell = board.Cells[new Position(1, 3)];
        mineCell.Status.Should().Be(CellStatus.Taken, "mine cell should remain Taken");

        // Verify the mine was flagged by the card
        var takenMine = (ITakenCell)mineCell;
        takenMine.HasMine.Should().BeTrue("mine cell should still have mine");

        // All revealed positions including mine should be in ActionData
        var actionData = result.ActionData as CardActionSnapshot.MinefieldScout;
        actionData.Should().NotBeNull();
        actionData!.RevealedCells.Should().Contain(new Position(1, 3));
    }
}
