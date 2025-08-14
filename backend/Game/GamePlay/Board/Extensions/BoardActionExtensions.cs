using Shared;

namespace Game.GamePlay;

public static class BoardActionExtensions
{
    public static void EnsureGenerated(this IBoard board, Position position)
    {
        if (board.Cells.Count != 0)
            return;
        
        board.Generator.Generate(position);
        board.Revealer.Reveal(position);
        board.OnUpdated();
    }
}