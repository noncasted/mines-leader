using Shared;

namespace Game.GamePlay;

public class EmbargoStrategy : IBotCardStrategy
{
    public EmbargoStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Embargo];

    public float Evaluate(CardType type)
    {
        var opponent = _context.Opponent;
        var opponentHand = opponent.Hand.Entries.Count;

        if (opponentHand >= 3)
            return 6f;

        return 3f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var payload = new CardUsePayload.Embargo { Type = cardType };
        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}