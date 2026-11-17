using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Могильщика (Gravedigger) - восстанавливает карты из запасов в руку.
/// Рандомный приоритет в зависимости от наличия карт в запасах.
/// </summary>
public class GravediggerStrategy : IBotCardStrategy
{
    public GravediggerStrategy(
        IBotContext context,
        IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Gravedigger];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var stashCount = bot.Stash.Count;

        if (stashCount == 0)
            return 0f;

        // Scale with stash size — more cards to recycle = more valuable
        // 1 card: 3, 2 cards: 5, 3+: 7
        // High priority when hand is small (need more options)
        var handSize = bot.Hand.Entries.Count;
        var bonus = handSize <= 2 ? 2f : 0f;

        return Math.Min(3f + stashCount * 2f + bonus, 9f);
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;

        var payload = new CardUsePayload.Gravedigger()
        {
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}