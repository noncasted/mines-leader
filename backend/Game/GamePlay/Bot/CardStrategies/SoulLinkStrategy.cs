using Shared;

namespace Game.GamePlay;

/// <summary>
/// SoulLink strategy - links damage between players for N rounds.
/// Priority 5 if HP >= 2.
/// </summary>
public class SoulLinkStrategy : IBotCardStrategy
{
    public SoulLinkStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.SoulLink];

    public float Evaluate(CardType type)
    {
        var hp = _context.Bot.Health.Current;

        if (hp < 2)
            return 0f;

        return 5f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var payload = new CardUsePayload.SoulLink
        {
            Type = cardType
        };

        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}