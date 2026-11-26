using Shared;

namespace Game.GamePlay;

/// <summary>
/// CardThief strategy - steals a random card from the opponent's hand.
/// Priority 6 if the opponent has cards.
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