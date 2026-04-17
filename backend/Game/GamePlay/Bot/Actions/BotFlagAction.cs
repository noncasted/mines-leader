using Game.Session;
using Shared;

namespace Game.GamePlay;

public interface IBotFlagAction
{
    bool TryExecute();
}

public class BotFlagAction : IBotFlagAction
{
    public BotFlagAction(IBotCommandUtils commandUtils, IBotContext context, ISessionLogger sessionLogger)
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
        var board = _context.Bot.Board;

        if (board.Cells.Count == 0)
        {
            var randomPosition = _context.Bot.Board.RandomPosition();
            board.EnsureGenerated(randomPosition);
            _sessionLogger.LogBotAction("Flag", $"Board empty, generated at {randomPosition}");
            return true;
        }

        if (TryGetMineToFlag(out var target, out var reason) == false)
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

    private bool TryGetMineToFlag(out Position target, out string reason)
    {
        var board = _context.Bot.Board;

        // Стратегия 1: Найти мину логически через constraint-solving
        // Ищем Free ячейку где количество заметённых флагов < количество мин вокруг
        foreach (var (position, cell) in board.Cells)
        {
            if (cell.Status != CellStatus.Free)
                continue;

            var freeCell = cell.AsFree();

            if (freeCell.MinesAround == 0)
                continue;

            var neighbourPositions = board.NeighbourPositions(position);

            var neighbours = neighbourPositions
                             .Where(p => board.Cells[p].Status == CellStatus.Taken)
                             .Select(p => board.Cells[p].AsTaken())
                             .ToList();

            if (neighbours.Count == 0)
                continue;

            foreach (var takenCell in neighbours)
            {
                if (takenCell.IsFlagged == true)
                    continue;

                if (takenCell.HasMine == false)
                    continue;

                target = takenCell.Position;
                reason = $"Constraint-solve near {position} (MinesAround={freeCell.MinesAround})";
                return true;
            }
        }

        target = new Position(-1, -1);
        reason = "No unflagged mines found by constraint-solving";
        return false;
    }
}