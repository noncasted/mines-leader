using Common.Reactive;
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

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.ZipZap, CardType.ZipZap_Max];

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

    public bool Execute(CardType cardType)
    {
        var position = _boardUtils.FindClosestUnflaggedMine();

        if (position == new Position(-1, -1))
            return false;

        var bot = _context.Bot;
        
        var payload = new CardUsePayload.ZipZap
        {
            Position = position,
            Type = cardType
        };
        
        return _commandUtils.UseCard(bot, payload);
    }
}
