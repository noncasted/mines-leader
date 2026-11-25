using Shared;

namespace Game.GamePlay;

/// <summary>
/// Medic strategy - restores 1 HP.
/// High priority when HP is critically low.
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
        var health = bot.Health.Current;
        var maxHealth = bot.Health.ResultMax;

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