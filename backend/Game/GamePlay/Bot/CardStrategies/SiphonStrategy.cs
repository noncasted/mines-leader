using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Сифона (Siphon) - снимает ману у противника.
/// Высокий приоритет когда у противника много маны.
/// </summary>
public class SiphonStrategy : IBotCardStrategy
{
    public SiphonStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Siphon];

    public float Evaluate(CardType type)
    {
        var opponent = _context.Opponent;
        var opponentMana = opponent.Mana.Current;

        if (opponentMana >= 4)
            return 7f;

        if (opponentMana >= 2)
            return 4f;

        return 1f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;

        var payload = new CardUsePayload.Siphon
        {
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}
