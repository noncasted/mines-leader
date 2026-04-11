using Shared;

namespace Game.GamePlay;

public class ManaFountainStrategy : IBotCardStrategy {
    public ManaFountainStrategy(IBotContext context, IBotCommandUtils commandUtils) {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.ManaFountain];

    public float Evaluate(CardType type) {
        var bot = _context.Bot;

        if (bot.Mana.Current <= 2)
            return 7f;

        return 4f;
    }

    public bool Execute(Guid cardId, CardType cardType) {
        var payload = new CardUsePayload.ManaFountain { Type = cardType };
        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}
