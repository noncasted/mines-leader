using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

public class CellStateTests
{
    [Fact]
    public void TakenCell_ToFree_CreatesFreeCell()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];
        taken.ToFree();

        board.Cells[pos].Status.Should().Be(CellStatus.Free);
    }

    [Fact]
    public void FreeCell_ToTaken_CreatesTakenCell()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t _ t
                                           t t t
                                           """);
        var pos = new Position(1, 1);

        var free = (IFreeCell)board.Cells[pos];
        free.ToTaken();

        board.Cells[pos].Status.Should().Be(CellStatus.Taken);
    }

    [Fact]
    public void TakenCell_ToTaken_ReturnsSelf()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];
        var result = taken.ToTaken();

        ReferenceEquals(result, taken).Should().BeTrue();
    }

    [Fact]
    public void FreeCell_ToFree_ReturnsSelf()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t _ t
                                           t t t
                                           """);
        var pos = new Position(1, 1);

        var free = (IFreeCell)board.Cells[pos];
        var result = free.ToFree();

        ReferenceEquals(result, free).Should().BeTrue();
    }

    [Fact]
    public void TakenCell_SetMine_SetsHasMine()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];
        taken.SetMine();

        taken.HasMine.Should().BeTrue();
    }

    [Fact]
    public void TakenCell_SetFlag_SetsIsFlagged()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];
        taken.SetFlag();

        taken.IsFlagged.Should().BeTrue();
    }

    [Fact]
    public void TakenCell_RemoveFlag_ClearsFlagged()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];
        taken.SetFlag();
        taken.RemoveFlag();

        taken.IsFlagged.Should().BeFalse();
    }

    [Fact]
    public void TakenCell_DefaultState_NoMineNoFlag()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];

        taken.HasMine.Should().BeFalse();
        taken.IsFlagged.Should().BeFalse();
    }

    [Fact]
    public void ToFree_UpdatesBoardCellsDictionary()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var pos = new Position(1, 1);

        var oldTaken = (ITakenCell)board.Cells[pos];
        var newFree = oldTaken.ToFree();

        board.Cells[pos].Should().BeSameAs(newFree);
        board.Cells[pos].Should().NotBeSameAs(oldTaken);
    }
}