using Shared;

namespace Game.GamePlay;

/// <summary>
/// FortuneCookie strategy - reveals mine positions on its own board.
/// Fixed priority 4.
/// </summary>
public class FortuneCookieStrategy : IBotCardStrategy
{
    public FortuneCookieStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.FortuneCookie];

    public float Evaluate(CardType type)
    {
        return 4f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var payload = new CardUsePayload.FortuneCookie
        {
            Type = cardType
        };

        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}