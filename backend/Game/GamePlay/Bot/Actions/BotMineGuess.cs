using Shared;

namespace Game.GamePlay;

public static class BotMineGuess
{
    public static bool TryFind(IBoard board, out Position target, out string reason)
    {
        var probabilities = new List<(Position pos, float probability)>();

        foreach (var (position, cell) in board.Cells)
        {
            if (cell.Status != CellStatus.Taken)
                continue;

            var taken = cell.AsTaken();
            if (taken.IsFlagged == true)
                continue;

            var freeNeighbors = board.NeighbourPositions(position)
                .Where(p => board.Cells.TryGetValue(p, out var neighbor) && neighbor.Status == CellStatus.Free)
                .Select(p => board.Cells[p].AsFree())
                .ToList();

            if (freeNeighbors.Count == 0)
                continue;

            float maxProb = 0f;
            foreach (var freeCell in freeNeighbors)
            {
                var freePos = freeCell.Position;
                var takenNeighbors = board.NeighbourPositions(freePos)
                    .Where(p => board.Cells.TryGetValue(p, out var n) && n.Status == CellStatus.Taken)
                    .Select(p => board.Cells[p].AsTaken())
                    .ToList();

                var flaggedCount = takenNeighbors.Count(t => t.IsFlagged == true);
                var unflaggedCount = takenNeighbors.Count(t => t.IsFlagged == false);

                if (unflaggedCount == 0)
                    continue;
                if (freeCell.MinesAround <= flaggedCount)
                    continue;

                var remainingMines = freeCell.MinesAround - flaggedCount;
                var prob = (float)remainingMines / unflaggedCount;
                if (prob > maxProb)
                    maxProb = prob;
            }

            if (maxProb > 0)
                probabilities.Add((position, maxProb));
        }

        if (probabilities.Count == 0)
        {
            var randomUnflagged = board.Cells.Values
                .Where(c => c.Status == CellStatus.Taken && !c.AsTaken().IsFlagged)
                .Select(c => c.Position)
                .ToList();

            if (randomUnflagged.Count == 0)
            {
                target = new Position(-1, -1);
                reason = "No unflagged cells remain";
                return false;
            }

            target = randomUnflagged[Random.Shared.Next(randomUnflagged.Count)];
            reason = "Random unflagged cell (no local constraints)";
            return true;
        }

        var best = probabilities.OrderBy(p => p.probability).First();
        target = best.pos;
        reason = $"Probabilistic-safe (P={best.probability:F2})";
        return true;
    }
}
