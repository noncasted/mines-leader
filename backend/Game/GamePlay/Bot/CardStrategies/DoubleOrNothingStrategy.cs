using Shared;

namespace Game.GamePlay;

public class DoubleOrNothingStrategy : IBotCardStrategy {
    public DoubleOrNothingStrategy(IBotContext context, IBotCommandUtils commandUtils) {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.DoubleOrNothing];

    public float Evaluate(CardType type) {
        var bot = _context.Bot;
        if (bot.Mana.Current >= 5)
            return 6f;
        if (bot.Mana.Current <= 2)
            return 1f;
        return 3f;
    }

    public bool Execute(Guid cardId, CardType cardType) {
        var payload = new CardUsePayload.DoubleOrNothing { Type = cardType };
        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}
