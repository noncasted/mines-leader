using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Мародёра (Scavenger) - берёт 2 карты из колоды.
/// Приоритет зависит от количества карт в руке.
/// </summary>
public class ScavengerStrategy : IBotCardStrategy
{
    public ScavengerStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Scavenger];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var handCount = bot.Hand.Entries.Count;
        var deckCount = bot.Deck.Count;

        if (deckCount == 0)
            return 0f;

        if (handCount <= 2)
            return 7f;

        return 3f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;

        var payload = new CardUsePayload.Scavenger
        {
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}