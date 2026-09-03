using Shared;

namespace Game.GamePlay;

/// <summary>
/// Честный выбор центра для карт с фигурой: считает только то, что видно игроку
/// (статус клетки, флаг, цифры), геометрию берёт из тех же PatternShapes, что и сама карта.
/// При равном счёте центр выбирается случайно, чтобы бот не бил всегда в одну точку.
/// </summary>
public static class BotCardTargeting
{
    public static readonly Position None = new(-1, -1);

    /// <summary>
    /// Bloodhound: ромб, который накрывает больше всего закрытых клеток без флага.
    /// Клетки фронта (рядом с открытой) считаются вдвое: открытые там цифры
    /// сразу соединяются с известной зоной и дают аккорды.
    /// </summary>
    public static Position BestBloodhoundCentre(IBoard board, int size)
    {
        var shape = PatternShapes.Rhombus(size);
        return Best(board, position => ScoreTaken(board, shape.SelectTaken(board, position)));
    }

    /// <summary>
    /// MinefieldScout: карта сама выбирает горизонталь или вертикаль по числу закрытых клеток,
    /// поэтому оценивается та линия, которую возьмёт карта.
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
    /// Trebuchet: ромб на доске противника, накрывающий больше всего открытых клеток.
    /// Клетки с цифрой считаются вдвое: закрыть цифру значит сломать вывод, который на ней строился.
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
    /// Sonar: ромб с наибольшей ожидаемой суммой мин без флага. Ожидание берётся только из цифр:
    /// каждая открытая цифра распределяет свои неотмеченные мины поровну между закрытыми
    /// соседями без флага, клетке достаётся максимум по соседним цифрам. Клетки вне фронта
    /// ожидания не имеют, поэтому Sonar не улетает в глубь закрытого поля.
    /// Счёт целочисленный в сотых долях мины.
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
    /// Сколько мин ещё не отмечено по видимым цифрам: сумма по открытым клеткам
    /// разницы между цифрой и числом флагов вокруг. Честная замена подсчёту настоящих мин.
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
