using Game.Session;
using Shared;

namespace Game.GamePlay;

public interface IBotCellAction
{
    bool TryExecute();
}

public class BotCellAction : IBotCellAction
{
    public BotCellAction(IBotCommandUtils commandUtils, IBotContext context, ISessionLogger sessionLogger)
    {
        _commandUtils = commandUtils;
        _context = context;
        _sessionLogger = sessionLogger;
    }

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

        if (board.Cells.Count == 0)
        {
            var randomPosition = _context.Bot.Board.RandomPosition();

            _commandUtils.WithSnapshot(() => {
                _context.Bot.Moves.OnUsed();
                board.Generator.Generate(randomPosition);
                board.Cells[randomPosition].ToFree();
                board.Revealer.Reveal(randomPosition);
                board.OnUpdated();
                _context.Bot.Actions.OnCellOpened();
            });

            _sessionLogger.LogBotAction("Cell", $"First cell opened at {randomPosition}");
            return true;
        }

        if (TryGetFirstTargetCell(out var target, out var reason) == false)
        {
            // Fallback: open a random safe cell (known to have no mine), only if HP > 1
            if (_context.Bot.Health.Current.Value > 1 && TryGetRandomSafeCell(out target, out reason) == false)
            {
                _sessionLogger.LogBotAction("Cell", $"No cell found | {reason}");
                return false;
            }
            else if (_context.Bot.Health.Current.Value <= 1)
            {
                _sessionLogger.LogBotAction("Cell", "No safe cell found, skipping (HP=1, too risky)");
                return false;
            }
        }

        _commandUtils.WithSnapshot(() => {
            var taken = board.Cells[target].AsTaken();

            _context.Bot.Moves.OnUsed();
            taken.ToFree();
            board.Revealer.Reveal(target);
            board.OnUpdated();
            _context.Bot.Actions.OnCellOpened();
        });

        _sessionLogger.LogBotAction("Cell", $"Opened {target} | {reason} | MovesLeft={_context.Bot.Moves.Left}");
        return true;
    }

    private bool TryGetFirstTargetCell(out Position target, out string reason)
    {
        var board = _context.Bot.Board;
        var candidateCount = 0;

        foreach (var (position, cell) in board.Cells)
        {
            if (cell.Status != CellStatus.Free)
                continue;

            var freeCell = cell.AsFree();

            if (freeCell.MinesAround == 0)
                continue;

            var flagsAround = board
                              .NeighbourPositions(position)
                              .Where(p => board.Cells[p].Status == CellStatus.Taken)
                              .Select(p => board.Cells[p].AsTaken())
                              .Count(t => t.IsFlagged == true);

            if (flagsAround == 0)
                continue;

            var notFlaggedNeighbours = board
                                       .NeighbourPositions(position)
                                       .Where(p => board.Cells[p].Status == CellStatus.Taken)
                                       .Where(p => board.Cells[p].AsTaken().IsFlagged == false)
                                       .ToList();

            foreach (var neighbourPosition in notFlaggedNeighbours)
            {
                var neighbourCell = board.Cells[neighbourPosition].AsTaken();

                if (neighbourCell.HasMine == false)
                {
                    candidateCount++;
                    target = neighbourPosition;

                    reason =
                        $"Safe neighbour of {position} (MinesAround={freeCell.MinesAround}, FlagsAround={flagsAround})";
                    return true;
                }
            }
        }

        target = new Position(-1, -1);
        reason = "No constraint-solvable safe cells";
        return false;
    }

    /// <summary>
    /// Fallback: find a random taken cell that is known to NOT have a mine.
    /// </summary>
    private bool TryGetRandomSafeCell(out Position target, out string reason)
    {
        var board = _context.Bot.Board;

        var safeCells = board.Cells.Values
                             .Where(c => c.IsTaken())
                             .Select(c => c.AsTaken())
                             .Where(c => c.IsFlagged == false && c.HasMine == false)
                             .ToList();

        if (safeCells.Count == 0)
        {
            target = new Position(-1, -1);
            reason = "No known safe cells for random open";
            return false;
        }

        var selected = safeCells[Random.Shared.Next(safeCells.Count)];
        target = selected.Position;
        reason = $"Random safe cell (no mine, {safeCells.Count} candidates)";
        return true;
    }
}