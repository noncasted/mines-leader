using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Перемешивания Руки (HandScramble) - случайно заменяет карты в руке противника.
/// Высокий приоритет когда у противника много карт в руке.
/// </summary>
public class HandScrambleStrategy : IBotCardStrategy
{
    public HandScrambleStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.HandScramble];

    public float Evaluate(CardType type)
    {
        var opponent = _context.Opponent;
        var handCount = opponent.Hand.Entries.Count;

        if (handCount == 0)
            return 0f;

        if (handCount >= 4)
            return 7f;

        return 3f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;

        var payload = new CardUsePayload.HandScramble
        {
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}
