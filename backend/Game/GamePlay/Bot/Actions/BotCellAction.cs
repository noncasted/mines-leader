using Common.Reactive;
using Shared;

namespace Game.GamePlay;

public interface IBotCellAction
{
    bool TryExecute();
}

public class BotCellAction : IBotCellAction
{
    public BotCellAction(IBotCommandUtils commandUtils, IBotContext context)
    {
        _commandUtils = commandUtils;
        _context = context;
    }

    private readonly IBotCommandUtils _commandUtils;
    private readonly IBotContext _context;

    public bool TryExecute()
    {
        var board = _context.Bot.Board;

        if (board.Cells.Count == 0)
        {
            var randomPosition = _context.Bot.Board.RandomPosition();

            _commandUtils.WithSnapshot(() =>
                {
                    _context.Bot.Moves.OnUsed();
                    board.Generator.Generate(randomPosition);
                    board.Cells[randomPosition].ToFree();
                    board.Revealer.Reveal(randomPosition);
                    board.OnUpdated();
                }
            );

            return true;
        }

        if (TryGetFirstTargetCell(out var target) == false)
            return false;

        _commandUtils.WithSnapshot(() =>
            {
                var taken = board.Cells[target].AsTaken();

                _context.Bot.Moves.OnUsed();
                taken.ToFree();
                board.Revealer.Reveal(target);
                board.OnUpdated();
            }
        );

        return true;
    }

    private bool TryGetFirstTargetCell(out Position target)
    {
        var board = _context.Bot.Board;

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
                    target = neighbourPosition;
                    return true;
                }
            }
        }

        target = new Position(-1, -1);
        return false;
    }
}