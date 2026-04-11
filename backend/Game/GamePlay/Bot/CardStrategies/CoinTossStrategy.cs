using Shared;

namespace Game.GamePlay;

public class CoinTossStrategy : IBotCardStrategy {
    public CoinTossStrategy(IBotContext context, IBotCommandUtils commandUtils) {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.CoinToss];

    public float Evaluate(CardType type) {
        var bot = _context.Bot;

        if (bot.Moves.Left >= 3)
            return 5f;

        if (bot.Moves.Left <= 1)
            return 2f;

        return 4f;
    }

    public bool Execute(Guid cardId, CardType cardType) {
        var payload = new CardUsePayload.CoinToss { Type = cardType };
        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}
