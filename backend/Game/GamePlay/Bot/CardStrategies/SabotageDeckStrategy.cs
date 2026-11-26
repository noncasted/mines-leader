using Shared;

namespace Game.GamePlay;

/// <summary>
/// SabotageDeck strategy - adds a Dud to the opponent's deck.
/// Fixed priority 5.
/// </summary>
public class SabotageDeckStrategy : IBotCardStrategy
{
    public SabotageDeckStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.SabotageDeck];

    public float Evaluate(CardType type)
    {
        return 5f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var payload = new CardUsePayload.SabotageDeck
        {
            Type = cardType
        };

        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}