using Shared;

namespace Game.GamePlay;

public static class CardActionCellsHelper
{
    public static HashSet<Position> CaptureTaken(IBoard board)
    {
        var result = new HashSet<Position>();

        foreach (var (position, cell) in board.Cells)
        {
            if (cell.Status == CellStatus.Taken)
                result.Add(position);
        }

        return result;
    }

    public static IReadOnlyList<Position> CollectOpened(IBoard board, HashSet<Position> takenBefore)
    {
        var opened = new List<Position>();

        foreach (var position in takenBefore)
        {
            if (board.Cells.TryGetValue(position, out var cell) == false)
                continue;

            if (cell.Status == CellStatus.Free)
                opened.Add(position);
        }

        return opened;
    }
}
