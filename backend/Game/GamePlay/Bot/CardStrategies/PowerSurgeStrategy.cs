using Shared;

namespace Game.GamePlay;

public class PowerSurgeStrategy : IBotCardStrategy {
    public PowerSurgeStrategy(IBotContext context, IBotCommandUtils commandUtils) {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.PowerSurge];

    public float Evaluate(CardType type) {
        var bot = _context.Bot;
        var handSize = bot.Hand.Entries.Count;

        if (handSize >= 3)
            return 7f;

        return 2f;
    }

    public bool Execute(Guid cardId, CardType cardType) {
        var payload = new CardUsePayload.PowerSurge { Type = cardType };
        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}
