using Shared;

namespace Game.GamePlay;

public class RecyclerStrategy : IBotCardStrategy {
    public RecyclerStrategy(IBotContext context, IBotCommandUtils commandUtils) {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Recycler];

    public float Evaluate(CardType type) {
        var bot = _context.Bot;
        if (bot.Hand.Entries.Count >= 4)
            return 5f;
        return 3f;
    }

    public bool Execute(Guid cardId, CardType cardType) {
        var bot = _context.Bot;
        var entries = bot.Hand.Entries;
        if (entries.Count == 0) return false;

        var discardId = entries[Random.Shared.Next(entries.Count)].Id;

        var payload = new CardUsePayload.Recycler {
            Type = cardType,
            DiscardCardId = discardId
        };
        return _commandUtils.UseCard(bot, cardId, payload);
    }
}
