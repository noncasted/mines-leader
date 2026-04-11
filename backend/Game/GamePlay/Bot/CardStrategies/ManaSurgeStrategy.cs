using Shared;

namespace Game.GamePlay;

public class ManaSurgeStrategy : IBotCardStrategy {
    public ManaSurgeStrategy(IBotContext context, IBotCommandUtils commandUtils) {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.ManaSurge];

    public float Evaluate(CardType type) {
        var bot = _context.Bot;
        var mana = bot.Mana.Current;

        if (mana <= 2)
            return 7f;

        return 4f;
    }

    public bool Execute(Guid cardId, CardType cardType) {
        var bot = _context.Bot;

        var payload = new CardUsePayload.ManaSurge {
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}
