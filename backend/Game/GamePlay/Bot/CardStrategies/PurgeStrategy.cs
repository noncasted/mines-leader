using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Очистки (Purge) - снимает все вражеские эффекты со своего поля.
/// Высокий приоритет когда есть активные эффекты на поле.
/// </summary>
public class PurgeStrategy : IBotCardStrategy
{
    public PurgeStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Purge];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var hasEffects = bot.Board.Cells.Values.Any(c => c.Effects.Count > 0);

        return hasEffects ? 9f : 0f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;

        var payload = new CardUsePayload.Purge
        {
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}