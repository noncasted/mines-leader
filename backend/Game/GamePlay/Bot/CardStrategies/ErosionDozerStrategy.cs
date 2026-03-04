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
        ICardFactory cardFactory,
        BotBoardUtils boardUtils,
        IBotCommandUtils commandUtils)
    {
        _context = context;
        _cardFactory = cardFactory;
        _boardUtils = boardUtils;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly ICardFactory _cardFactory;
    private readonly BotBoardUtils _boardUtils;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.ErosionDozer, CardType.ErosionDozer_Max];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var totalCells = bot.Board.Cells.Count;
        var closedCells = bot.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken);

        // Если закрыто больше 50% поля - высокий приоритет
        if (closedCells > totalCells * 0.5)
            return 8f;

        return 2f;
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