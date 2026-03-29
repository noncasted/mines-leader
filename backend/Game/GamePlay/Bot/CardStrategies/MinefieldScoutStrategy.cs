using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Разведчика Минного Поля (MinefieldScout) - открывает линию клеток.
/// Приоритет зависит от количества закрытых клеток.
/// </summary>
public class MinefieldScoutStrategy : IBotCardStrategy
{
    public MinefieldScoutStrategy(IBotContext context, BotBoardUtils boardUtils, IBotCommandUtils commandUtils)
    {
        _context = context;
        _boardUtils = boardUtils;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly BotBoardUtils _boardUtils;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.MinefieldScout, CardType.MinefieldScout_Max];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var totalCells = bot.Board.Cells.Count;
        var closedCells = bot.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken);

        if (closedCells > totalCells * 0.6)
            return 7f;

        return 3f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var position = _boardUtils.FindRandomTakenPosition();

        if (position == new Position(-1, -1))
            return false;

        var bot = _context.Bot;

        var payload = new CardUsePayload.MinefieldScout
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}
