using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Саботажа колоды (SabotageDeck) - добавляет Dud в колоду противника.
/// Фиксированный приоритет 5.
/// </summary>
public class SabotageDeckStrategy : IBotCardStrategy
{
    public SabotageDeckStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.SabotageDeck];

    public float Evaluate(CardType type)
    {
        return 5f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var payload = new CardUsePayload.SabotageDeck
        {
            Type = cardType
        };

        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}
