using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Tests BoardMinesScanner — calculates MinesAround for Free cells
/// by counting mines in Taken neighbours (all 8 directions).
/// </summary>
public class MinesScannerTests
{
    [Fact]
    public void FreeCell_OneMineNeighbour_MinesAroundIsOne()
    {
        var (board, _) = BoardParser.Parse("""
                                           t m t
                                           t _ t
                                           t t t
                                           """);

        var freeCell = (IFreeCell)board.Cells[new Position(1, 1)];
        freeCell.MinesAround.Should().Be(1);
    }

    [Fact]
    public void FreeCell_NoMineNeighbours_MinesAroundIsZero()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t _ t
                                           t t t
                                           """);

        var freeCell = (IFreeCell)board.Cells[new Position(1, 1)];
        freeCell.MinesAround.Should().Be(0);
    }

    [Fact]
    public void FreeCell_DiagonalMines_CountsCorrectly()
    {
        var (board, _) = BoardParser.Parse("""
                                           m t t
                                           t _ t
                                           t t m
                                           """);

        var freeCell = (IFreeCell)board.Cells[new Position(1, 1)];
        freeCell.MinesAround.Should().Be(2);
    }

    [Fact]
    public void FreeCell_SurroundedByMines_MinesAroundIsEight()
    {
        var (board, _) = BoardParser.Parse("""
                                           m m m
                                           m _ m
                                           m m m
                                           """);

        var freeCell = (IFreeCell)board.Cells[new Position(1, 1)];
        freeCell.MinesAround.Should().Be(8);
    }

    [Fact]
    public void RecalculatesAfterCellStatusChange()
    {
        // Start with all Taken, mine at (1,0)
        var (board, _) = BoardParser.Parse("""
                                           t m t
                                           t t t
                                           t t t
                                           """);

        // Convert cell at (1,1) to Free after build
        board.Cells[new Position(1, 1)].ToTaken().ToFree();
        board.MinesScanner.Recalculate();

        var freeCell = (IFreeCell)board.Cells[new Position(1, 1)];
        freeCell.MinesAround.Should().Be(1);
    }

    [Fact]
    public void TakenCells_NotScanned()
    {
        // All Taken cells with mines — no Free cells, MinesScanner should skip everything
        var (board, _) = BoardParser.Parse("""
                                           m m m
                                           m t m
                                           m m m
                                           """);

        // All cells should remain Taken — no crash
        foreach (var (_, cell) in board.Cells)
        {
            cell.Status.Should().Be(CellStatus.Taken);

            cell.Should()
                .BeAssignableTo<ITakenCell>("all cells are Taken, none should have been converted to Free");
        }

        // No cell should be IFreeCell (MinesScanner only computes MinesAround for Free cells)
        board.Cells.Values.OfType<IFreeCell>()
             .Should()
             .BeEmpty("scanner skips Taken cells — no Free cells should exist");
    }

    [Fact]
    public void MineInTakenCell_CountedByAdjacentFreeCell()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t m t t
                                           t t _ t t
                                           t t t t t
                                           t t t t t
                                           """);

        var freeCell = (IFreeCell)board.Cells[new Position(2, 2)];
        freeCell.MinesAround.Should().Be(1);
    }
}