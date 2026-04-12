using Shared;

namespace Game.GamePlay;

public class AdrenalineStrategy : IBotCardStrategy
{
    public AdrenalineStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Adrenaline];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var movesLeft = bot.Moves.Left;

        if (movesLeft <= 1)
            return 9f;

        if (movesLeft >= 3)
            return 6f;

        return 4f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;

        var payload = new CardUsePayload.Adrenaline
        {
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}