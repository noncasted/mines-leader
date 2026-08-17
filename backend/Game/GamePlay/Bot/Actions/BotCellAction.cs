using Cluster.Configs;
using Game.GamePlay.Snapshots;
using Game.Session;
using Shared;

namespace Game.GamePlay;

public interface IBotCellAction
{
    bool TryExecute();
}

public class BotCellAction : IBotCellAction
{
    public BotCellAction(
        IBotConfig config,
        IBotCommandUtils commandUtils,
        IBotContext context,
        ISessionLogger sessionLogger)
    {
        _config = config;
        _commandUtils = commandUtils;
        _context = context;
        _sessionLogger = sessionLogger;
    }

    private readonly IBotConfig _config;
    private readonly IBotCommandUtils _commandUtils;
    private readonly IBotContext _context;
    private readonly ISessionLogger _sessionLogger;

    private readonly record struct OpenPlan(IReadOnlyList<Position> Seeds, int Gain, string Reason);

    public bool TryExecute()
    {
        if (_context.Bot.Moves.Left <= 0)
        {
            _sessionLogger.LogBotAction("Cell", "No moves left");
            return false;
        }

        var board = _context.Bot.Board;
        var depth = GetConstraintDepth();

        if (board.Cells.Count == 0)
        {
            var randomPosition = board.RandomPosition();

            _commandUtils.WithSnapshot(snapshot => {
                _context.Bot.Moves.OnUsed(snapshot);

                board.Generator.Generate(randomPosition);
                snapshot.RecordReveal(board, randomPosition);

                _context.Bot.Actions.OnCellOpened();
            });

            _sessionLogger.LogBotAction("Cell", $"First cell opened at {randomPosition}");
            return true;
        }

        // Аккорд и одиночное открытие стоят один ход, поэтому выбираем по числу клеток,
        // которые реально откроются вместе с каскадом.
        var chord = FindBestChord();
        var single = FindBestSafeCell(depth);

        var plan = chord?.Gain >= (single?.Gain ?? 0) ? chord : single;

        if (plan != null)
            return Open(plan.Value);

        if (_config.Value.CurrentProfile == BotProfile.Hard &&
            TryProbabilisticSafeCell(out var target, out var reason))
        {
            return Open(new OpenPlan([target], 1, reason));
        }

        _sessionLogger.LogBotAction("Cell", "No safe cell found | No constraint-proven safe cells");
        return false;
    }

    private bool Open(OpenPlan plan)
    {
        var board = _context.Bot.Board;

        _commandUtils.WithSnapshot(snapshot => {
            _context.Bot.Moves.OnUsed(snapshot);

            foreach (var position in plan.Seeds)
            {
                var cell = board.Cells[position];

                if (cell.Status != CellStatus.Taken)
                    continue;

                var taken = cell.AsTaken();

                if (taken.HasMine == false)
                    continue;

                _context.Bot.Health.TakeDamage(snapshot, 1);
                snapshot.RecordExplosion(board, position);
                taken.Explode();
                taken.ToFree();
                board.RegisterDetonatedMine();
                snapshot.RecordCellFree(board, position);
            }

            snapshot.RecordReveal(board, plan.Seeds);

            _context.Bot.Actions.OnCellOpened();
        });

        _sessionLogger.LogBotAction("Cell",
            $"Opened {plan.Seeds.Count} seed(s) at {plan.Seeds[0]} | {plan.Reason} | MovesLeft={_context.Bot.Moves.Left}");

        return true;
    }

    private int GetConstraintDepth()
    {
        return _config.Value.CurrentProfile == BotProfile.Easy ? 1 : 2;
    }

    /// <summary>
    /// Аккорд: открытая клетка, у которой все мины уже отмечены флагами,
    /// раскрывает всех оставшихся закрытых соседей за один ход.
    /// </summary>
    private OpenPlan? FindBestChord()
    {
        var board = _context.Bot.Board;
        OpenPlan? best = null;

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

                // Ложный флаг где-то рядом — аккорд взорвёт нас, пропускаем.
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

            var gain = EstimateOpen(targets);

            if (best != null && gain <= best.Value.Gain)
                continue;

            best = new OpenPlan(targets, gain, $"Chord at {position} ({targets.Count} cells, {gain} with cascade)");
        }

        return best;
    }

    private OpenPlan? FindBestSafeCell(int depth)
    {
        var candidates = CollectProvenSafe(depth);

        if (candidates.Count == 0)
            return null;

        var best = candidates[0];
        var bestGain = EstimateOpen([best]);

        foreach (var candidate in candidates)
        {
            var gain = EstimateOpen([candidate]);

            if (gain <= bestGain)
                continue;

            best = candidate;
            bestGain = gain;
        }

        return new OpenPlan(
            [best],
            bestGain,
            $"Proven-safe, best cascade ({bestGain} cells of {candidates.Count} candidates)");
    }

    /// <summary>
    /// Все клетки, безопасность которых доказана ограничениями.
    /// Порядок перемешан, чтобы при равном каскаде бот не ходил всегда в один и тот же угол.
    /// </summary>
    private List<Position> CollectProvenSafe(int depth)
    {
        var board = _context.Bot.Board;
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

    /// <summary>
    /// Сколько клеток реально откроется, если раскрыть переданные клетки —
    /// с учётом волны от клеток без мин вокруг. Повторяет логику BoardRevealer.
    /// </summary>
    private int EstimateOpen(IReadOnlyList<Position> seeds)
    {
        var board = _context.Bot.Board;
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

            if (MinesAround(current) != 0)
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

    private int MinesAround(Position position)
    {
        var board = _context.Bot.Board;
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

    private bool TryProbabilisticSafeCell(out Position target, out string reason)
    {
        var board = _context.Bot.Board;
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
