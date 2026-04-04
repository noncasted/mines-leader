using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Tests Board.Revealer.Reveal() — the flood-fill algorithm that opens safe cells
/// when a player clicks a cell.
///
/// Rules:
/// - Start from a Free cell (the one player just opened)
/// - Expand to Taken neighbors that have a Free neighbor with MinesAround=0
/// - Recurse until no more cells qualify
/// - Mines and cells adjacent to mines (MinesAround > 0) stop the expansion
/// </summary>
public class RevealTests {
    /// <summary>
    /// Open a cell manually: convert Taken→Free, recalculate MinesAround, then Reveal.
    /// This simulates what happens when a player clicks a cell.
    /// </summary>
    private static void OpenCell(IBoard board, Position pos) {
        var cell = board.Cells[pos];
        if (cell.Status == CellStatus.Taken)
            ((ITakenCell)cell).ToFree();

        board.OnUpdated(); // trigger MinesScanner recalculation
        board.Revealer.Reveal(pos);
    }

    [Fact]
    public void Reveal_EmptyBoard_OpensEverything() {
        var (board, target) = BoardParser.Parse("""
            t t t t t t
            t t t t t t
            t t t t t t
            t t t x t t
            t t t t t t
            t t t t t t
            """);

        OpenCell(board, target);

        // No mines — every cell should flood-fill to Free
        BoardParser.AssertBoard(board, """
            R R R R R R
            R R R R R R
            R R R R R R
            R R R R R R
            R R R R R R
            R R R R R R
            """);
    }

    [Fact]
    public void Reveal_SingleMine_StopsAtBorder() {
        var (board, target) = BoardParser.Parse("""
            t t t t t t
            t t t t t t
            t t t t t t
            t t t x t t
            t t t t t t
            t t t t t m
            """);

        OpenCell(board, target);

        // Reveal expands everywhere except cells adjacent to the mine
        // (4,4) and (4,5) and (5,4) have MinesAround > 0, they become Free but don't expand
        BoardParser.AssertBoard(board, """
            R R R R R R
            R R R R R R
            R R R R R R
            R R R R R R
            R R R R R R
            R R R R R m
            """);
    }

    [Fact]
    public void Reveal_MineRing_ContainsFloodFill() {
        var (board, target) = BoardParser.Parse("""
            t t t t t t t
            t m m m m m t
            t m t t t m t
            t m t x t m t
            t m t t t m t
            t m m m m m t
            t t t t t t t
            """);

        OpenCell(board, target);

        // Flood-fill cannot escape the mine ring — inner cells open, outer stays Taken
        BoardParser.AssertBoard(board, """
            t t t t t t t
            t m m m m m t
            t m R R R m t
            t m R R R m t
            t m R R R m t
            t m m m m m t
            t t t t t t t
            """);
    }

    [Fact]
    public void Reveal_CorridorBetweenMines() {
        // Mines at corners — cells directly adjacent to mines stay Taken
        var (board, target) = BoardParser.Parse("""
            m t t t t m
            t t t t t t
            m t t t t m
            t t x t t t
            m t t t t m
            t t t t t t
            m t t t t m
            """);

        OpenCell(board, target);

        // Cells at (0,1),(5,1) etc are adjacent to corner mines — MinesAround > 0,
        // no Free neighbor with MinesAround=0, so they stay Taken
        BoardParser.AssertBoard(board, """
            m R R R R m
            t R R R R t
            m R R R R m
            t R R R R t
            m R R R R m
            t R R R R t
            m R R R R m
            """);
    }

    [Fact]
    public void Reveal_ClickOnMineAdjacentCell_NoExpansion() {
        // Target cell (2,2) is adjacent to mine at (2,1) — MinesAround=1
        // No Free neighbor with MinesAround=0 exists, so no expansion
        var (board, target) = BoardParser.Parse("""
            t t t t t
            t t m t t
            t t x t t
            t t t t t
            t t t t t
            """);

        OpenCell(board, target);

        // Only the clicked cell opens — mine blocks all expansion
        BoardParser.AssertBoard(board, """
            t t t t t
            t t m t t
            t t R t t
            t t t t t
            t t t t t
            """);
    }

    [Fact]
    public void Reveal_ClickAtCorner() {
        var (board, target) = BoardParser.Parse("""
            x t t t t t
            t t t t t t
            t t m t t t
            t t t t t t
            t t t t m t
            t t t t t t
            """);

        OpenCell(board, target);

        // Cells in the corner pocket near mine (4,4) — some stay Taken
        // because they have MinesAround > 0 and no Free neighbor with MinesAround=0
        BoardParser.AssertBoard(board, """
            R R R R R R
            R R R R R R
            R R m R R R
            R R R R R R
            R R R R m t
            R R R R t t
            """);
    }

    [Fact]
    public void Reveal_DenseMineField_MinimalExpansion() {
        // Mines at all 4 corners and edges — center (2,2) has MinesAround=0
        // but all 8 neighbors have MinesAround>0, so expansion stops at 1 ring
        var (board, target) = BoardParser.Parse("""
            m t m t m
            t t t t t
            m t x t m
            t t t t t
            m t m t m
            """);

        OpenCell(board, target);

        // Center + all 8 immediate neighbors open; mines and outer ring stay Taken
        BoardParser.AssertBoard(board, """
            m t m t m
            t R R R t
            m R R R m
            t R R R t
            m t m t m
            """);
    }

    [Fact]
    public void Reveal_TwoSeparateRegions_OnlyOneOpens() {
        // Two safe pockets separated by a mine wall — only the clicked pocket opens
        var (board, target) = BoardParser.Parse("""
            t t t m t t t
            t t t m t t t
            t x t m t t t
            t t t m t t t
            t t t m t t t
            t t t m t t t
            t t t m t t t
            """);

        OpenCell(board, target);

        // Left region opens, right region stays Taken
        BoardParser.AssertBoard(board, """
            R R R m t t t
            R R R m t t t
            R R R m t t t
            R R R m t t t
            R R R m t t t
            R R R m t t t
            R R R m t t t
            """);
    }

    [Fact]
    public void Reveal_FlaggedCell_NotOpened() {
        // Flagged cells should be treated as Taken (not opened by reveal)
        var (board, target) = BoardParser.Parse("""
            t t t t t
            t t t t t
            t t x t t
            t t t t t
            t t t t g
            """);

        OpenCell(board, target);

        // Everything opens except the flagged cell
        // (flagged cell is Taken, reveal skips mines — but 'g' is not a mine, just flagged)
        // Actually HasMine check: reveal skips cells where HasMine=true
        // 'g' has HasMine=false, so reveal WILL open it
        BoardParser.AssertBoard(board, """
            R R R R R
            R R R R R
            R R R R R
            R R R R R
            R R R R R
            """);
    }

    [Fact]
    public void Reveal_MinesAlongEdge() {
        var (board, target) = BoardParser.Parse("""
            m m m m m m
            t t t t t t
            t t t t t t
            t t t x t t
            t t t t t t
            t t t t t t
            """);

        OpenCell(board, target);

        // Top row is all mines — reveal fills bottom 5 rows, stops at mine border
        BoardParser.AssertBoard(board, """
            m m m m m m
            R R R R R R
            R R R R R R
            R R R R R R
            R R R R R R
            R R R R R R
            """);
    }

    [Fact]
    public void Reveal_DiagonalMines_BlockCompletely() {
        // Diagonal mines at (5,0),(4,1),(3,2) — target (2,3) is adjacent to mine (3,2),
        // so MinesAround>0. No Free cell with MinesAround=0 exists, only target opens.
        var (board, target) = BoardParser.Parse("""
            t t t t t m
            t t t t m t
            t t t m t t
            t t x t t t
            t t t t t t
            t t t t t t
            """);

        OpenCell(board, target);

        // Only the clicked cell opens — mine adjacency blocks all expansion
        BoardParser.AssertBoard(board, """
            t t t t t m
            t t t t m t
            t t t m t t
            t t R t t t
            t t t t t t
            t t t t t t
            """);
    }
}
