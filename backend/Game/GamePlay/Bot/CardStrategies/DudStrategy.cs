using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Пустышки (Dud) - карта-заглушка, никогда не используется ботом.
/// Приоритет 0.
/// </summary>
public class DudStrategy : IBotCardStrategy
{
    public DudStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Dud];

    public float Evaluate(CardType type)
    {
        return 0f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        return false;
    }
}
