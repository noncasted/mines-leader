using Shared;

namespace Game.GamePlay;

public class GamblersRuinStrategy : IBotCardStrategy {
    public GamblersRuinStrategy(IBotContext context, IBotCommandUtils commandUtils) {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.GamblersRuin];

    public float Evaluate(CardType type) {
        var bot = _context.Bot;
        if (bot.Hand.Entries.Count <= 1)
            return 5f;
        return 3f;
    }

    public bool Execute(Guid cardId, CardType cardType) {
        var payload = new CardUsePayload.GamblersRuin { Type = cardType };
        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}
