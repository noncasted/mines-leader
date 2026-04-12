using Shared;

namespace Game.GamePlay;

public class BloodPactStrategy : IBotCardStrategy
{
    public BloodPactStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.BloodPact];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;

        if (bot.Health.Current.Value <= 1)
            return 0f;

        if (bot.Mana.Current <= 1 && bot.Moves.Left <= 1)
            return 8f;

        return 4f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var payload = new CardUsePayload.BloodPact { Type = cardType };
        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}