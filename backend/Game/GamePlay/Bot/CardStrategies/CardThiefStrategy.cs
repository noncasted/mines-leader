using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Вора карт (CardThief) - крадёт случайную карту из руки противника.
/// Приоритет 6 если у противника есть карты.
/// </summary>
public class CardThiefStrategy : IBotCardStrategy
{
    public CardThiefStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.CardThief];

    public float Evaluate(CardType type)
    {
        if (_context.Opponent.Hand.Entries.Count == 0)
            return 0f;

        return 6f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var payload = new CardUsePayload.CardThief
        {
            Type = cardType
        };

        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}