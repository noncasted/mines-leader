using Shared;

namespace Game.GamePlay;

public static class BotOpenCascade
{
    /// <summary>
    /// Сколько клеток реально откроется, если раскрыть переданные клетки —
    /// с учётом волны от клеток без мин вокруг. Повторяет логику BoardRevealer.
    /// </summary>
    public static int Estimate(IBoard board, IReadOnlyList<Position> seeds)
    {
        var opened = new HashSet<Position>();
        var queue = new Queue<Position>();

        foreach (var seed in seeds)
        {
            if (board.Cells.TryGetValue(seed, out var cell) == false)
                continue;

            if (cell.Status != CellStatus.Taken || cell.AsTaken().HasMine == true)
                continue;

            if (opened.Add(seed) == true)
                queue.Enqueue(seed);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (MinesAround(board, current) != 0)
                continue;

            foreach (var neighbour in board.NeighbourPositions(current))
            {
                var cell = board.Cells[neighbour];

                if (cell.Status != CellStatus.Taken || cell.AsTaken().HasMine == true)
                    continue;

                if (opened.Add(neighbour) == true)
                    queue.Enqueue(neighbour);
            }
        }

        return opened.Count;
    }

    private static int MinesAround(IBoard board, Position position)
    {
        var count = 0;

        foreach (var neighbour in board.NeighbourPositions(position))
        {
            var cell = board.Cells[neighbour];

            if (cell.Status != CellStatus.Taken)
                continue;

            if (cell.AsTaken().HasMine == true)
                count++;
        }

        return count;
    }
}
