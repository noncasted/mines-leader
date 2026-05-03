using Cluster.Configs;
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
            var randomPosition = _context.Bot.Board.RandomPosition();

            _commandUtils.WithSnapshot(snapshot => {
                _context.Bot.Moves.OnUsed(snapshot);

                board.Generator.Generate(randomPosition);
                snapshot.RecordReveal(board, randomPosition);

                _context.Bot.Actions.OnCellOpened();
            });

            _sessionLogger.LogBotAction("Cell", $"First cell opened at {randomPosition}");
            return true;
        }

        if (TryGetSafeCell(depth, out var target, out var reason) == false)
        {
            if (_config.Value.CurrentProfile == BotProfile.Hard && TryProbabilisticSafeCell(out target, out reason))
            {
                // Hard bot falls back to lowest-probability cell
            }
            else
            {
                _sessionLogger.LogBotAction("Cell", $"No safe cell found | {reason}");
                return false;
            }
        }

        _commandUtils.WithSnapshot(snapshot => {
            var taken = board.Cells[target].AsTaken();

            _context.Bot.Moves.OnUsed(snapshot);

            if (taken.HasMine == true)
            {
                _context.Bot.Health.TakeDamage(snapshot, 1);
                snapshot.RecordExplosion(board, target);
                taken.Explode();
                taken.ToFree();
                snapshot.RecordCellFree(board, target);
            }

            snapshot.RecordReveal(board, target);

            _context.Bot.Actions.OnCellOpened();
        });

        _sessionLogger.LogBotAction("Cell", $"Opened {target} | {reason} | MovesLeft={_context.Bot.Moves.Left}");
        return true;
    }

    private int GetConstraintDepth()
    {
        return _config.Value.CurrentProfile == BotProfile.Easy ? 1 : 2;
    }

    private bool TryGetSafeCell(int depth, out Position target, out string reason)
    {
        if (TryLevel1Safe(out target, out reason))
            return true;

        if (depth >= 2 && TryLevel2Safe(out target, out reason))
            return true;

        if (TryRandomProvenSafe(depth, out target, out reason))
            return true;

        target = new Position(-1, -1);
        reason = "No constraint-proven safe cells";
        return false;
    }

    private bool TryLevel1Safe(out Position target, out string reason)
    {
        var board = _context.Bot.Board;

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

            if (freeCell.MinesAround == flaggedCount && unflaggedNeighbors.Count > 0)
            {
                var allSafe = unflaggedNeighbors.All(t => t.HasMine == false);
                if (allSafe)
                {
                    target = unflaggedNeighbors[0].Position;
                    reason = $"Safe neighbour of {position} (all mines flagged)";
                    return true;
                }
            }
        }

        target = new Position(-1, -1);
        reason = "No level-1 safe cells";
        return false;
    }

    private bool TryLevel2Safe(out Position target, out string reason)
    {
        var board = _context.Bot.Board;

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

                if (minesDiff == 0)
                {
                    var allSafe = unflaggedDiff.All(p => !board.Cells[p].AsTaken().HasMine);
                    if (allSafe)
                    {
                        target = unflaggedDiff[0];
                        reason = $"Subset-safe near {a.Pos}/{b.Pos} (diff={unflaggedDiff.Count})";
                        return true;
                    }
                }
            }
        }

        target = new Position(-1, -1);
        reason = "No level-2 safe cells";
        return false;
    }

    private bool TryRandomProvenSafe(int depth, out Position target, out string reason)
    {
        var board = _context.Bot.Board;
        var safeCells = new List<Position>();

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

            if (freeCell.MinesAround == flaggedCount)
            {
                safeCells.AddRange(unflaggedNeighbors
                    .Where(t => t.HasMine == false)
                    .Select(t => t.Position));
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

                    if (minesDiff == 0)
                    {
                        safeCells.AddRange(unflaggedDiff.Where(p => !board.Cells[p].AsTaken().HasMine));
                    }
                }
            }
        }

        safeCells = safeCells.Distinct().ToList();

        if (safeCells.Count == 0)
        {
            target = new Position(-1, -1);
            reason = "No constraint-proven safe cells";
            return false;
        }

        target = safeCells[Random.Shared.Next(safeCells.Count)];
        reason = $"Random proven-safe cell ({safeCells.Count} candidates)";
        return true;
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
