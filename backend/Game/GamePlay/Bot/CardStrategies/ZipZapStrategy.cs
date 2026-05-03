using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия ЗипЗапа (ZipZap) - целевое уничтожение конкретной мины на нашей доске.
/// Высокий приоритет если закрыто больше 50% поля.
/// </summary>
public class ZipZapStrategy : IBotCardStrategy
{
    public ZipZapStrategy(
        IBotContext context,
        BotBoardUtils boardUtils,
        IBotCommandUtils commandUtils)
    {
        _context = context;
        _boardUtils = boardUtils;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly BotBoardUtils _boardUtils;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.ZipZap, CardType.ZipZap_Max];

    public float Evaluate(CardType type)
    {
        if (FindMineTarget() == new Position(-1, -1))
            return 0f;

        var bot = _context.Bot;
        var totalCells = bot.Board.Cells.Count;

        if (totalCells == 0)
            return 0f;

        var closedCells = bot.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken);
        var closedRatio = (float)closedCells / totalCells;

        // ZipZap destroys mines — always valuable when target exists
        // Higher priority than ErosionDozer since it removes threats
        // Scale: 9 at 80%+ closed → 4 at 20% closed
        return 4f + closedRatio * 6.5f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var position = FindMineTarget();

        if (position == new Position(-1, -1))
            return false;

        var bot = _context.Bot;

        var payload = new CardUsePayload.ZipZap
        {
            Position = position,
            Type = cardType,
            CardId = cardId
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }

    /// <summary>
    /// Find a free cell that has an unflagged mine nearby.
    /// ZipZap needs a free position as its pattern anchor; the card
    /// then searches for mines within SearchRadius of that anchor.
    /// </summary>
    private Position FindMineTarget()
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
                .Where(p => board.Cells.TryGetValue(p, out var neighbor) && neighbor.Status == CellStatus.Taken)
                .Select(p => board.Cells[p].AsTaken())
                .ToList();

            var flaggedCount = takenNeighbors.Count(t => t.IsFlagged == true);
            if (freeCell.MinesAround == flaggedCount)
                continue; // All mines around this cell are already flagged

            return position;
        }

        return new Position(-1, -1);
    }
}