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
        ICardFactory cardFactory,
        IBotCommandUtils commandUtils)
    {
        _context = context;
        _cardFactory = cardFactory;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly ICardFactory _cardFactory;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Gravedigger];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;

        // Полезна только если в стэше есть карты
        var stashCount = bot.Stash.Count;
        if (stashCount == 0)
            return 0f;

        // Рандомный приоритет между 3 и 6
        return Random.Shared.Next(3, 7);
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