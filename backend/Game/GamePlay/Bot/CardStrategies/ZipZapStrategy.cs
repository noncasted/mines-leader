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
    /// Find an unflagged mine that has at least one adjacent free cell.
    /// Returns the mine position directly — the card will find it via SearchRadius.
    /// </summary>
    private Position FindMineTarget()
    {
        var board = _context.Bot.Board;

        foreach (var (position, cell) in board.Cells)
        {
            if (cell.IsTaken() == false)
                continue;

            var taken = cell.AsTaken();

            if (taken.IsFlagged == true)
                continue;

            if (taken.HasMine == false)
                continue;

            // Verify there's at least one adjacent free cell (so card's SelectFree won't fail)
            var neighbours = board.NeighbourPositions(position);
            var hasAdjacentFree = neighbours.Any(n => board.Cells.TryGetValue(n, out var nc) && nc.IsFree());

            if (hasAdjacentFree)
                return position;
        }

        return new Position(-1, -1);
    }
}