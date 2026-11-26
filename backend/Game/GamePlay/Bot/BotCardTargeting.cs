using Shared;

namespace Game.GamePlay;

/// <summary>
/// Honest choice of the center for cards with a shape: counts only what the player sees
/// (cell status, flag, numbers), takes geometry from the same PatternShapes as the card itself.
/// On equal score the center is random, so the bot does not always hit the same spot.
/// </summary>
public static class BotCardTargeting
{
    public static readonly Position None = new(-1, -1);

    /// <summary>
    /// Bloodhound: the diamond covering the most closed cells without a flag.
    /// Frontier cells (next to an open one) count double: the numbers opened there
    /// connect to the known zone at once and give chords.
    /// </summary>
    public static Position BestBloodhoundCentre(IBoard board, int size)
    {
        var shape = PatternShapes.Rhombus(size);
        return Best(board, position => ScoreTaken(board, shape.SelectTaken(board, position)));
    }

    /// <summary>
    /// MinefieldScout: the card picks horizontal or vertical by the number of closed cells,
    /// so the line the card will take is the one evaluated.
    /// </summary>
    public static Position BestMinefieldScoutCentre(IBoard board, int size)
    {
        var horizontal = PatternShapes.Line(size, horizontal: true);
        var vertical = PatternShapes.Line(size, horizontal: false);

        return Best(board, position => {
            var horizontalCells = horizontal.SelectTaken(board, position);
            var verticalCells = vertical.SelectTaken(board, position);
            var chosen = horizontalCells.Count >= verticalCells.Count ? horizontalCells : verticalCells;
            return ScoreTaken(board, chosen);
        });
    }

    /// <summary>
    /// Trebuchet: the diamond on the opponent's board covering the most open cells.
    /// Cells with a number count double: closing a number breaks the deduction built on it.
    /// </summary>
    public static Position BestTrebuchetCentre(IBoard opponentBoard, int size)
    {
        var shape = PatternShapes.Rhombus(size);

        return Best(opponentBoard, position => {
            var score = 0;

            foreach (var cell in shape.SelectFree(opponentBoard, position))
            {
                score++;

                if (cell.AsFree().MinesAround > 0)
                    score++;
            }

            return score;
        });
    }

    /// <summary>
    /// Sonar: the diamond with the highest expected sum of mines without a flag. The expectation comes only from numbers:
    /// each open number spreads its unmarked mines evenly among the closed
    /// neighbors without a flag, a cell gets the maximum over the neighboring numbers. Cells outside the frontier
    /// have no expectation, so Sonar does not fly deep into the closed field.
    /// The score is an integer in hundredths of a mine.
    /// </summary>
    public static Position BestSonarCentre(IBoard board, int size)
    {
        var shape = PatternShapes.Rhombus(size);
        var estimate = EstimateMines(board);

        return Best(board, position => {
            var score = 0f;

            foreach (var cell in shape.SelectTaken(board, position))
            {
                if (cell.IsFlagged == true)
                    continue;

                if (estimate.TryGetValue(cell.Position, out var chance) == true)
                    score += chance;
            }

            return (int)Math.Round(score * 100f);
        });
    }

    /// <summary>
    /// How many mines are still unmarked according to the visible numbers: the sum over open cells of
    /// the difference between the number and the flags around. An honest replacement for counting real mines.
    /// </summary>
    public static int UnresolvedMinesByNumbers(IBoard board)
    {
        var total = 0;

        foreach (var (position, cell) in board.Cells)
        {
            if (cell.Status != CellStatus.Free)
                continue;

            var remaining = cell.AsFree().MinesAround - FlaggedAround(board, position);

            if (remaining > 0)
                total += remaining;
        }

        return total;
    }

    private static Dictionary<Position, float> EstimateMines(IBoard board)
    {
        var estimate = new Dictionary<Position, float>();

        foreach (var (position, cell) in board.Cells)
        {
            if (cell.Status != CellStatus.Free)
                continue;

            var unknown = new List<Position>();
            var flagged = 0;

            foreach (var neighbour in board.NeighbourPositions(position))
            {
                var neighbourCell = board.Cells[neighbour];

                if (neighbourCell.Status != CellStatus.Taken)
                    continue;

                if (neighbourCell.AsTaken().IsFlagged == true)
                    flagged++;
                else
                    unknown.Add(neighbour);
            }

            var remaining = cell.AsFree().MinesAround - flagged;

            if (remaining <= 0 || unknown.Count == 0)
                continue;

            var chance = (float)remaining / unknown.Count;

            foreach (var neighbour in unknown)
            {
                if (estimate.TryGetValue(neighbour, out var current) == false || chance > current)
                    estimate[neighbour] = chance;
            }
        }

        return estimate;
    }

    private static int FlaggedAround(IBoard board, Position position)
    {
        var flagged = 0;

        foreach (var neighbour in board.NeighbourPositions(position))
        {
            var cell = board.Cells[neighbour];

            if (cell.Status == CellStatus.Taken && cell.AsTaken().IsFlagged == true)
                flagged++;
        }

        return flagged;
    }

    private static int ScoreTaken(IBoard board, IReadOnlyList<ITakenCell> cells)
    {
        var score = 0;

        foreach (var cell in cells)
        {
            if (cell.IsFlagged == true)
                continue;

            score++;

            if (IsFrontier(board, cell.Position) == true)
                score++;
        }

        return score;
    }

    private static bool IsFrontier(IBoard board, Position position)
    {
        foreach (var neighbour in board.NeighbourPositions(position))
        {
            if (board.Cells[neighbour].Status == CellStatus.Free)
                return true;
        }

        return false;
    }

    private static Position Best(IBoard board, Func<Position, int> score)
    {
        if (board.IsGenerated == false)
            return None;

        var bestScore = 0;
        var best = new List<Position>();

        for (var y = 0; y < board.Size.y; y++)
        {
            for (var x = 0; x < board.Size.x; x++)
            {
                var position = new Position(x, y);
                var value = score(position);

                if (value <= 0 || value < bestScore)
                    continue;

                if (value > bestScore)
                {
                    bestScore = value;
                    best.Clear();
                }

                best.Add(position);
            }
        }

        if (best.Count == 0)
            return None;

        return best[Random.Shared.Next(best.Count)];
    }
}
