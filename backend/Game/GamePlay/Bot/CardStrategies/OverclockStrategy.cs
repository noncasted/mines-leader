using Shared;

namespace Game.GamePlay;

/// <summary>
/// Overclock strategy - adds 2 moves.
/// High priority at the start of a turn when there are enough moves for a combo.
/// </summary>
public class OverclockStrategy : IBotCardStrategy
{
    public OverclockStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Overclock];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var movesLeft = bot.Moves.Left;

        if (movesLeft >= 3)
            return 8f;

        return 3f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;

        var payload = new CardUsePayload.Overclock
        {
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}