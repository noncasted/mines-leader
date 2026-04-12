using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Стирания Флагов Противника (OpponentFlagErase) - удаляет один флаг противника.
/// Нарушает планы противника и заставляет его переоценивать доску.
/// Полезна если у противника много флагов.
/// </summary>
public class OpponentFlagEraseStrategy : IBotCardStrategy
{
    public OpponentFlagEraseStrategy(
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

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.OpponentFlagErase, CardType.OpponentFlagErase_Max];

    public float Evaluate(CardType type)
    {
        var opponent = _context.Opponent;

        // Полезна только если у противника есть флаги
        if (!_boardUtils.HasFlaggedCells(opponent: true))
            return 0f;

        var flaggedCount =
            opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken && c.AsTaken().IsFlagged);

        // Чем больше флагов у противника, тем выше полезность
        if (flaggedCount > 10)
            return 7f; // Много флагов - наносим помеху

        if (flaggedCount > 5)
            return 5f;

        return 3f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        if (!_boardUtils.HasFlaggedCells(opponent: true))
            return false;

        var position = _boardUtils.FindRandomFlaggedPosition(opponent: true);

        if (position == new Position(-1, -1))
            return false;

        var bot = _context.Bot;

        var payload = new CardUsePayload.OpponentFlagErase
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}