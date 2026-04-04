using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Блокировки (Lockdown) - уменьшает макс. ходы противника на 2 раунда.
/// Высокий приоритет когда у противника много ходов.
/// </summary>
public class LockdownStrategy : IBotCardStrategy
{
    public LockdownStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Lockdown];

    public float Evaluate(CardType type)
    {
        var opponent = _context.Opponent;
        var opponentMoves = opponent.Moves.Max;

        if (opponentMoves >= 3)
            return 7f;

        if (opponentMoves >= 2)
            return 4f;

        return 1f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;

        var payload = new CardUsePayload.Lockdown
        {
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}