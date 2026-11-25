using Shared;

namespace Game.GamePlay;

public static class BotProvenSafeCells
{
    /// <summary>
    /// Все клетки, безопасность которых доказана ограничениями.
    /// Порядок перемешан, чтобы при равном каскаде бот не ходил всегда в один и тот же угол.
    /// </summary>
    public static List<Position> Collect(IBoard board, int depth)
    {
        var safeCells = new HashSet<Position>();

        foreach (var (position, cell) in board.Cells)
        {
            if (cell.Status != CellStatus.Free)
                continue;

            var freeCell = cell.AsFree();

            if (freeCell.MinesAround == 0)
                continue;

            var takenNeighbors = board
                .NeighbourPositions(position)
                .Where(p => board.Cells[p].Status == CellStatus.Taken)
                .Select(p => board.Cells[p].AsTaken())
                .ToList();

            var flaggedCount = takenNeighbors.Count(t => t.IsFlagged == true);
            var unflaggedNeighbors = takenNeighbors.Where(t => t.IsFlagged == false).ToList();

            if (freeCell.MinesAround != flaggedCount)
                continue;

            foreach (var neighbour in unflaggedNeighbors)
            {
                if (neighbour.HasMine == false)
                    safeCells.Add(neighbour.Position);
            }
        }

        if (depth >= 2)
        {
            var freeCells = board.Cells.Values
                .Where(c => c.Status == CellStatus.Free)
                .Select(c => new { Pos = c.Position, c.AsFree().MinesAround })
                .ToList();

            foreach (var a in freeCells)
            {
                var neighborsA = board.NeighbourPositions(a.Pos)
                    .Where(p => board.Cells[p].Status == CellStatus.Taken)
                    .ToList();

                foreach (var b in freeCells)
                {
                    if (a.Pos == b.Pos)
                        continue;

                    var neighborsB = board.NeighbourPositions(b.Pos)
                        .Where(p => board.Cells[p].Status == CellStatus.Taken)
                        .ToList();

                    if (neighborsB.Count >= neighborsA.Count)
                        continue;

                    if (!neighborsB.All(nb => neighborsA.Contains(nb)))
                        continue;

                    var diff = neighborsA.Except(neighborsB).ToList();
                    var flagsInDiff = diff.Count(p => board.Cells[p].AsTaken().IsFlagged);
                    var unflaggedDiff = diff.Where(p => !board.Cells[p].AsTaken().IsFlagged).ToList();

                    if (unflaggedDiff.Count == 0)
                        continue;

                    var minesDiff = a.MinesAround - b.MinesAround - flagsInDiff;

                    if (minesDiff != 0)
                        continue;

                    foreach (var position in unflaggedDiff)
                    {
                        if (board.Cells[position].AsTaken().HasMine == false)
                            safeCells.Add(position);
                    }
                }
            }
        }

        var result = safeCells.ToList();

        for (var i = result.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return result;
    }
}
