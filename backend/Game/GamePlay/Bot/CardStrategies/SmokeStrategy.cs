using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Дыма (Smoke) - скрывает клетку противника.
/// Рандомный приоритет.
/// </summary>
public class SmokeStrategy : IBotCardStrategy
{
    public SmokeStrategy(
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

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Smoke, CardType.Smoke_Max];

    public float Evaluate(CardType type)
    {
        var opponent = _context.Opponent;

        // Проверяем есть ли клетки у противника
        var opponentTakenCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken);
        if (opponentTakenCount == 0)
            return 0f;

        // Рандомный приоритет между 1 и 4
        return Random.Shared.Next(1, 5);
    }

    public bool Execute(CardType cardType)
    {
        var position = _boardUtils.FindRandomTakenPosition(opponent: true);

        if (position == new Position(-1, -1))
            return false;

        var bot = _context.Bot;

        var payload = new CardUsePayload.Smoke
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, payload);
    }
}