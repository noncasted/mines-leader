namespace Game.GamePlay;

public static class CellExtensions
{
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
}