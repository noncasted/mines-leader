using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Разведчика Минного Поля (MinefieldScout) - открывает линию клеток.
/// Приоритет зависит от количества закрытых клеток.
/// </summary>
public class MinefieldScoutStrategy : IBotCardStrategy
{
    public MinefieldScoutStrategy(IBotContext context, ICardConfigs cardConfigs, IBotCommandUtils commandUtils)
    {
        _context = context;
        _cardConfigs = cardConfigs;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly ICardConfigs _cardConfigs;
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
        // Карта всегда берёт размер Normal, даже для Max: повторяем это при прицеливании.
        var size = _cardConfigs.Value.MinefieldScout_Normal.Size;
        var position = BotCardTargeting.BestMinefieldScoutCentre(_context.Bot.Board, size);

        if (position == BotCardTargeting.None)
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