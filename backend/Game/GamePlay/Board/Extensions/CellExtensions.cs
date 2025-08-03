using Shared;

namespace Game.GamePlay;

public static class CellExtensions
{
    public static bool IsTaken(this ICell cell)
    {
        return cell.Status == CellStatus.Taken;
    }
    
    public static bool IsFree(this ICell cell)
    {
        return cell.Status == CellStatus.Taken;
    }
    
    public static ITakenCell AsTaken(this ICell cell)
    {
        if (cell is not ITakenCell taken)
            throw new InvalidCastException($"Cell at position {cell.Position} is not a taken cell.");

        return taken;
    }
    
    public static IFreeCell AsFree(this ICell cell)
    {
        if (cell is not IFreeCell free)
            throw new InvalidCastException($"Cell at position {cell.Position} is not a free cell.");

        return free;
    }
    
    public static IReadOnlyList<ICell> SelectTaken(this IPattenShape shape, IBoard board, Position center)
    {
        return shape.Select(board, center, cell => cell.IsTaken());
    }

    public static IReadOnlyList<ICell> SelectFree(this IPattenShape shape, IBoard board, Position center)
    {
        return shape.Select(board, center, cell => cell.IsFree());
    }

    public static IReadOnlyList<ICell> Select(
        this IPattenShape shape,
        IBoard board,
        Position center,
        Func<ICell, bool> filter
    )
    {
        if (center == new Position(-1, -1))
            return [];

        var size = shape.Positions.Count;
        var halfSize = size / 2;
        var start = center - new Position(halfSize, halfSize);

        var selected = new List<ICell>();

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (shape.Positions[y][x] == false)
                    continue;

                var position = start + new Position(x, y);

                if (board.Cells.TryGetValue(position, out var cell) == false)
                    continue;

                if (filter(cell) == false)
                    continue;

                selected.Add(board.Cells[position]);
            }
        }

        return selected;
    }
}