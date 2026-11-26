using Shared;

namespace Game.GamePlay;

public static class BotOpenPlans
{
    /// <summary>
    /// Chord: an open cell whose mines are all marked with flags
    /// reveals all remaining closed neighbors in one move.
    /// </summary>
    public static BotOpenPlan? FindBestChord(IBoard board)
    {
        BotOpenPlan? best = null;

        foreach (var (position, cell) in board.Cells)
        {
            if (cell.Status != CellStatus.Free)
                continue;

            var free = cell.AsFree();

            if (free.MinesAround == 0)
                continue;

            var flagged = 0;
            var targets = new List<Position>();
            var blocked = false;

            foreach (var neighbour in board.NeighbourPositions(position))
            {
                var neighbourCell = board.Cells[neighbour];

                if (neighbourCell.Status != CellStatus.Taken)
                    continue;

                var taken = neighbourCell.AsTaken();

                if (taken.IsFlagged == true)
                {
                    flagged++;
                    continue;
                }

                // A wrong flag nearby: the chord would blow us up, skip.
                if (taken.HasMine == true)
                {
                    blocked = true;
                    break;
                }

                targets.Add(neighbour);
            }

            if (blocked == true || targets.Count == 0)
                continue;

            if (free.MinesAround != flagged)
                continue;

            var gain = BotOpenCascade.Estimate(board, targets);

            if (best != null && gain <= best.Value.Gain)
                continue;

            best = new BotOpenPlan(targets, gain, $"Chord at {position} ({targets.Count} cells, {gain} with cascade)");
        }

        return best;
    }

    public static BotOpenPlan? FindBestSafeCell(IBoard board, int depth)
    {
        var candidates = BotProvenSafeCells.Collect(board, depth);

        if (candidates.Count == 0)
            return null;

        var best = candidates[0];
        var bestGain = BotOpenCascade.Estimate(board, [best]);

        foreach (var candidate in candidates)
        {
            var gain = BotOpenCascade.Estimate(board, [candidate]);

            if (gain <= bestGain)
                continue;

            best = candidate;
            bestGain = gain;
        }

        return new BotOpenPlan(
            [best],
            bestGain,
            $"Proven-safe, best cascade ({bestGain} cells of {candidates.Count} candidates)");
    }
}
