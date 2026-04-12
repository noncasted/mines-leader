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
        BotBoardUtils boardUtils,
        IBotCommandUtils commandUtils)
    {
        _context = context;
        _boardUtils = boardUtils;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly BotBoardUtils _boardUtils;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Trebuchet, CardType.Trebuchet_Max];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var opponent = _context.Opponent;

        // Проверяем есть ли вообще закрытые клетки у противника
        var opponentTakenCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken);

        if (opponentTakenCount == 0)
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
        var position = _boardUtils.FindRandomTakenPosition(opponent: true);

        if (position == new Position(-1, -1))
            return false;

        var bot = _context.Bot;

        var payload = new CardUsePayload.Trebuchet
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}