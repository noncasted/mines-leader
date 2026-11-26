using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// MinefieldScout strategy - opens a line of cells.
/// Priority depends on the number of closed cells.
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
        // The card always takes the Normal size, even for Max: repeat that when aiming.
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