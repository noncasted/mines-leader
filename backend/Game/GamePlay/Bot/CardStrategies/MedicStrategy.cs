using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Медика (Medic) - восстанавливает 1 HP.
/// Высокий приоритет когда HP критически низкий.
/// </summary>
public class MedicStrategy : IBotCardStrategy
{
    public MedicStrategy(IBotContext context, IBotCommandUtils commandUtils)
    {
        _context = context;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Medic];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var health = bot.Health.Current.Value;
        var maxHealth = bot.Health.Max;

        if (health >= maxHealth)
            return 0f;

        if (health == 1)
            return 10f;

        return 5f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;

        var payload = new CardUsePayload.Medic
        {
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}