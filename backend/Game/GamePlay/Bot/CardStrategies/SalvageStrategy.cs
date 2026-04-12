using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Утилизации (Salvage) - просматривает верхние карты колоды и берёт одну.
/// Фиксированный приоритет 4.
/// </summary>
public class SalvageStrategy : IBotCardStrategy
{
    public SalvageStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Salvage];

    public float Evaluate(CardType type)
    {
        if (_context.Bot.Deck.Count == 0)
            return 0f;

        return 4f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var payload = new CardUsePayload.Salvage
        {
            Type = cardType,
            ChosenIndex = 0
        };

        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}