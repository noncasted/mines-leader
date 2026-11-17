using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Требушета (Trebuchet) - дальнобойная атака на доску противника.
/// Максимальный приоритет если активен модификатор TrebuchetAimer.
/// Высокий приоритет если противник открыл больше клеток чем мы.
/// </summary>
public class TrebuchetStrategy : IBotCardStrategy
{
    public TrebuchetStrategy(
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

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Trebuchet, CardType.Trebuchet_Max];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var opponent = _context.Opponent;

        // Проверяем есть ли вообще закрытые клетки у противника
        var opponentTakenCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken);
        var opponentFreeCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);

        if (opponentTakenCount == 0 || opponentFreeCount == 0)
            return 0f;

        // Если активен модификатор TrebuchetAimer - максимальный приоритет
        if (bot.Modifiers.Values[PlayerModifier.TrebuchetBoost] > 0)
            return 10f;

        // Проверяем открыли ли противник больше клеток чем мы
        var botOpenCount = bot.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);
        var opponentOpenCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);

        if (opponentOpenCount > botOpenCount)
            return 7f; // Высокий приоритет - противник впереди

        return 2f; // Низкий приоритет - мы впереди
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;

        // Тот же размер, что посчитает карта: базовый ромб плюс усиление TrebuchetAimer.
        var size = _cardConfigs.Value.Trebuchet_Normal.Size +
                   (int)bot.Modifiers.Values[PlayerModifier.TrebuchetBoost] * 2;

        var position = BotCardTargeting.BestTrebuchetCentre(_context.Opponent.Board, size);

        if (position == BotCardTargeting.None)
            return false;

        var payload = new CardUsePayload.Trebuchet
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}