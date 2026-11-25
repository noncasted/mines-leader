using Shared;

namespace Game.GamePlay;

/// <summary>
/// OpponentFlagErase strategy - removes one opponent flag.
/// Breaks the opponent's plans and forces them to re-evaluate the board.
/// Useful if the opponent has many flags.
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

        if (!_boardUtils.HasFlaggedCells(opponent: true))
            return 0f;

        var flaggedCount =
            opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken && c.AsTaken().IsFlagged);

        // The more flags the opponent has, the more useful the card
        if (flaggedCount > 10)
            return 7f; // Many flags - we get in the way

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