using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Зеркального матча (MirrorMatch) - копирует последнюю карту противника.
/// Фиксированный приоритет 5.
/// </summary>
public class MirrorMatchStrategy : IBotCardStrategy
{
    public MirrorMatchStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.MirrorMatch];

    public float Evaluate(CardType type)
    {
        return 5f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var payload = new CardUsePayload.MirrorMatch
        {
            Type = cardType
        };

        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}
