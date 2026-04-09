using Shared;

namespace Game.GamePlay;

/// <summary>
/// Утилиты для анализа доски при использовании карт.
/// Все методы берут данные из IBotContext через конструктор.
/// </summary>
public class BotBoardUtils
{
    public BotBoardUtils(IBotContext context)
    {
        _context = context;
    }

    private readonly IBotContext _context;

    public Position RandomPosition()
    {
        var size = _context.Bot.Board.Size;
        var x = Random.Shared.Next(size.x);
        var y = Random.Shared.Next(size.y);
        return new Position(x, y);
    }

    public Position FindRandomClosedCell()
    {
        var board = _context.Bot.Board;

        var closedCells = board.Cells
                               .Where(kvp => kvp.Value.Status == CellStatus.Taken)
                               .Where(kvp => !kvp.Value.AsTaken().IsFlagged)
                               .Select(kvp => kvp.Key)
                               .ToList();

        if (closedCells.Count == 0)
            return new Position(-1, -1);

        var randomIndex = Random.Shared.Next(closedCells.Count);
        return closedCells[randomIndex];
    }

    public Position FindRandomTakenPosition(bool opponent = false)
    {
        var board = opponent ? _context.Opponent.Board : _context.Bot.Board;

        var takenCells = board.Cells
                              .Where(kvp => kvp.Value.Status == CellStatus.Taken)
                              .Select(kvp => kvp.Key)
                              .ToList();

        if (takenCells.Count == 0)
            return new Position(-1, -1);

        var randomIndex = Random.Shared.Next(takenCells.Count);
        return takenCells[randomIndex];
    }

    public Position FindClosestUnflaggedMine()
    {
        var board = _context.Bot.Board;

        var unflaggedMines = board.Cells
                                  .Where(kvp => kvp.Value.Status == CellStatus.Taken)
                                  .Where(kvp => !kvp.Value.AsTaken().IsFlagged)
                                  .Where(kvp => kvp.Value.AsTaken().HasMine)
                                  .Select(kvp => kvp.Key)
                                  .ToList();

        if (unflaggedMines.Count == 0)
            return new Position(-1, -1);

        return unflaggedMines[0];
    }

    public Position FindRandomFlaggedPosition(bool opponent = false)
    {
        var board = opponent ? _context.Opponent.Board : _context.Bot.Board;

        var flaggedCells = board.Cells
                                .Where(kvp => kvp.Value.Status == CellStatus.Taken)
                                .Where(kvp => kvp.Value.AsTaken().IsFlagged)
                                .Select(kvp => kvp.Key)
                                .ToList();

        if (flaggedCells.Count == 0)
            return new Position(-1, -1);

        var randomIndex = Random.Shared.Next(flaggedCells.Count);
        return flaggedCells[randomIndex];
    }

    public Position FindRandomFreePosition(bool opponent = false)
    {
        var board = opponent ? _context.Opponent.Board : _context.Bot.Board;

        var freeCells = board.Cells
                             .Where(kvp => kvp.Value.Status == CellStatus.Free)
                             .Select(kvp => kvp.Key)
                             .ToList();

        if (freeCells.Count == 0)
            return new Position(-1, -1);

        var randomIndex = Random.Shared.Next(freeCells.Count);
        return freeCells[randomIndex];
    }

    public bool HasFlaggedCells(bool opponent = false)
    {
        var board = opponent ? _context.Opponent.Board : _context.Bot.Board;
        return board.Cells.Values.Any(cell => cell.Status == CellStatus.Taken && cell.AsTaken().IsFlagged);
    }
}