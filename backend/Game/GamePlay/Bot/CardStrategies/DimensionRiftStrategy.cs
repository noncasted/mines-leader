using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Разлома измерений (DimensionRift) - меняет местами области доски.
/// Фиксированный приоритет 4.
/// </summary>
public class DimensionRiftStrategy : IBotCardStrategy
{
    public DimensionRiftStrategy(
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

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.DimensionRift];

    public float Evaluate(CardType type)
    {
        var opponentTakenCount = _context.Opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken);

        if (opponentTakenCount == 0)
            return 0f;

        return 4f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var position = _boardUtils.FindRandomTakenPosition(opponent: true);

        if (position == new Position(-1, -1))
            return false;

        var payload = new CardUsePayload.DimensionRift
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}
