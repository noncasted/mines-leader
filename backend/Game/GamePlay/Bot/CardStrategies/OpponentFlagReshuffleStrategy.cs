using Shared;

namespace Game.GamePlay;

/// <summary>
/// OpponentFlagReshuffle strategy - shuffles the opponent's flags.
/// High priority if the opponent has placed ~50% of flags relative to the number of mines.
/// </summary>
public class OpponentFlagReshuffleStrategy : IBotCardStrategy
{
    public OpponentFlagReshuffleStrategy(
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

    public IReadOnlyList<CardType> TargetCards { get; } =
        [CardType.OpponentFlagReshuffle, CardType.OpponentFlagReshuffle_Max];

    public float Evaluate(CardType type)
    {
        var opponent = _context.Opponent;

        if (!_boardUtils.HasFlaggedCells(opponent: true))
            return 0f;

        var flaggedCount =
            opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken && c.AsTaken().IsFlagged);

        var minesCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken && c.AsTaken().HasMine);

        if (minesCount == 0)
            return 0f;

        // If the opponent has placed about 50% of flags relative to mines
        var flagRatio = (float)flaggedCount / minesCount;

        if (Math.Abs(flagRatio - 0.5f) < 0.3f) // 20-80% of flags
            return 8f; // High priority - we break their strategy

        return 3f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        if (!_boardUtils.HasFlaggedCells(opponent: true))
            return false;

        var bot = _context.Bot;
        var position = _boardUtils.FindRandomFlaggedPosition(opponent: true);

        var payload = new CardUsePayload.OpponentFlagReshuffle
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}