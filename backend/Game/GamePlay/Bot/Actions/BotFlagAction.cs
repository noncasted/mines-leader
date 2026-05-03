using Cluster.Configs;
using Game.Session;
using Shared;

namespace Game.GamePlay;

public interface IBotFlagAction
{
    bool TryExecute();
}

public class BotFlagAction : IBotFlagAction
{
    public BotFlagAction(
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
        var board = _context.Bot.Board;
        var depth = GetConstraintDepth();

        if (board.Cells.Count == 0)
        {
            var randomPosition = _context.Bot.Board.RandomPosition();
            board.EnsureGenerated(randomPosition);
            _sessionLogger.LogBotAction("Flag", $"Board empty, generated at {randomPosition}");
            return true;
        }

        if (TryGetMineToFlag(depth, out var target, out var reason) == false)
        {
            _sessionLogger.LogBotAction("Flag", $"No mine to flag | {reason}");
            return false;
        }

        _commandUtils.WithSnapshot(snapshot => {
            var taken = board.Cells[target].AsTaken();

            taken.SetFlag();
            snapshot.RecordFlag(board, target, true);
            snapshot.RecordMines(board, board.MinesScanner.Recalculate(snapshot));
        });

        _sessionLogger.LogBotAction("Flag", $"Placed at {target} | {reason}");

        return true;
    }

    private int GetConstraintDepth()
    {
        return _config.Value.CurrentProfile == BotProfile.Easy ? 1 : 2;
    }

    private bool TryGetMineToFlag(int depth, out Position target, out string reason)
    {
        if (TryLevel1(out target, out reason))
            return true;

        if (depth >= 2 && TryLevel2(out target, out reason))
            return true;

        target = new Position(-1, -1);
        reason = "No constraint-solvable mines";
        return false;
    }

    private bool TryLevel1(out Position target, out string reason)
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

            if (unflaggedNeighbors.Count == 0)
                continue;

            var minesRemaining = freeCell.MinesAround - flaggedCount;

            if (minesRemaining == unflaggedNeighbors.Count)
            {
                var allMines = unflaggedNeighbors.All(t => t.HasMine == true);
                if (allMines)
                {
                    target = unflaggedNeighbors[0].Position;
                    reason = $"Constraint solve at {position} (MinesAround={freeCell.MinesAround}, Flagged={flaggedCount})";
                    return true;
                }
            }
        }

        target = new Position(-1, -1);
        reason = "No level-1 constraint-solvable mines";
        return false;
    }

    private bool TryLevel2(out Position target, out string reason)
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

                if (minesDiff == unflaggedDiff.Count)
                {
                    var allMines = unflaggedDiff.All(p => board.Cells[p].AsTaken().HasMine);
                    if (allMines)
                    {
                        target = unflaggedDiff[0];
                        reason = $"Subset solve near {a.Pos}/{b.Pos} (diff={unflaggedDiff.Count})";
                        return true;
                    }
                }
            }
        }

        target = new Position(-1, -1);
        reason = "No level-2 constraint-solvable mines";
        return false;
    }
}
