using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Эрозионного Бульдозера (ErosionDozer) - разрушает клетку на нашей доске.
/// Высокий приоритет если закрыто больше 50% поля.
/// </summary>
public class ErosionDozerStrategy : IBotCardStrategy
{
    public ErosionDozerStrategy(
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

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.ErosionDozer, CardType.ErosionDozer_Max];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var totalCells = bot.Board.Cells.Count;

        if (totalCells == 0)
            return 0f;

        var closedCells = bot.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken);
        var closedRatio = (float)closedCells / totalCells;

        // ErosionDozer opens cells in area — useful when lots of closed cells
        // More expensive than Bloodhound — slightly lower base to prefer cheap cards
        // Scale: 8.5 at 80%+ closed → 2 at 20% closed
        return 2f + closedRatio * 8f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;
        var position = _boardUtils.FindRandomTakenPosition();

        if (position == new Position(-1, -1))
            return false;

        var payload = new CardUsePayload.ErosionDozer
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}