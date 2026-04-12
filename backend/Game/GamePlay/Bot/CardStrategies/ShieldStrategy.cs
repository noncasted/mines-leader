using Shared;

namespace Game.GamePlay;

public class ShieldStrategy : IBotCardStrategy
{
    public ShieldStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Shield];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;

        if (bot.Health.Current.Value <= 1)
            return 8f;
        return 3f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var payload = new CardUsePayload.Shield { Type = cardType };
        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}