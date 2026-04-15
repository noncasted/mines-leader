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
        var opponentTotal = opponent.Board.Cells.Count;

        if (opponentTotal == 0)
            return 0f;

        var opponentOpenCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);
        var openRatio = (float)opponentOpenCount / opponentTotal;

        // Smoke hides opponent's open cells — most useful when opponent is ahead
        // Higher utility when opponent has opened a lot (more targets to disrupt)
        if (openRatio > 0.6f)
            return 6f;

        if (openRatio > 0.3f)
            return 4f;

        return 2f;
    }

    public bool Execute(Guid cardId, CardType cardType)
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

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}