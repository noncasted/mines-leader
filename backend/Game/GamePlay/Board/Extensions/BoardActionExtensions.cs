using Shared;

namespace Game.GamePlay;

public static class BoardActionExtensions
{
    public static void EnsureGenerated(this IBoard board, MoveSnapshot snapshot, Position position)
    {
        if (board.IsGenerated == true)
            return;

        board.Generator.Generate(position);
        snapshot.RecordBoardGenerated(board);
    }
}