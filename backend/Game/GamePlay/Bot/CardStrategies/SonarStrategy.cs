using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Сонара (Sonar) - автоматически флажит мины в области своего поля.
/// Приоритет и цель считаются только по видимым цифрам: сколько мин ещё не отмечено
/// и где по ограничениям их ожидается больше всего.
/// </summary>
public class SonarStrategy : IBotCardStrategy
{
    public SonarStrategy(IBotContext context, ICardConfigs cardConfigs, IBotCommandUtils commandUtils)
    {
        _context = context;
        _cardConfigs = cardConfigs;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly ICardConfigs _cardConfigs;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Sonar];

    public float Evaluate(CardType type)
    {
        var unflaggedMines = BotCardTargeting.UnresolvedMinesByNumbers(_context.Bot.Board);

        if (unflaggedMines >= 5)
            return 8f;

        if (unflaggedMines >= 2)
            return 5f;

        if (unflaggedMines > 0)
            return 2f;

        return 0f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var size = _cardConfigs.Value.Sonar_Normal.Size;
        var position = BotCardTargeting.BestSonarCentre(_context.Bot.Board, size);

        if (position == BotCardTargeting.None)
            return false;

        var bot = _context.Bot;

        var payload = new CardUsePayload.Sonar
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}