using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Ищейки (Bloodhound) - выявляет флаги на нашей доске, раскрывая скрытые мины.
/// Высокий приоритет если закрыто больше 50% поля (нужно быстрее открываться).
/// </summary>
public class BloodhoundStrategy : IBotCardStrategy
{
    public BloodhoundStrategy(
        IBotContext context,
        ICardConfigs cardConfigs,
        IBotCommandUtils commandUtils)
    {
        _context = context;
        _cardConfigs = cardConfigs;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly ICardConfigs _cardConfigs;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Bloodhound, CardType.Bloodhound_Max];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var totalCells = bot.Board.Cells.Count;

        if (totalCells == 0)
            return 0f;

        var closedCells = bot.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken);
        var closedRatio = (float)closedCells / totalCells;

        // Bloodhound reveals mine info — most useful early when board is unknown
        // Cheap card (2 mana) — good for mana efficiency
        // Scale: 9 at 80%+ closed → 3 at 20% closed
        return 3f + closedRatio * 7.5f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;
        var config = cardType == CardType.Bloodhound_Max
            ? _cardConfigs.Value.BloodHound_Max
            : _cardConfigs.Value.BloodHound_Normal;

        var position = BotCardTargeting.BestBloodhoundCentre(bot.Board, config.Size);

        if (position == BotCardTargeting.None)
            return false;

        var payload = new CardUsePayload.Bloodhound
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}