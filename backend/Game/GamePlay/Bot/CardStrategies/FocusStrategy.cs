using Shared;

namespace Game.GamePlay;

public class FocusStrategy : IBotCardStrategy {
    public FocusStrategy(IBotContext context, IBotCommandUtils commandUtils) {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Focus];

    public float Evaluate(CardType type) {
        var bot = _context.Bot;
        if (bot.Mana.Current <= 2)
            return 5f;
        return 3f;
    }

    public bool Execute(Guid cardId, CardType cardType) {
        var payload = new CardUsePayload.Focus { Type = cardType };
        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}
