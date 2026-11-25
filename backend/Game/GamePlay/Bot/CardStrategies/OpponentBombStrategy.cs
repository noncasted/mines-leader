using Shared;

namespace Game.GamePlay;

/// <summary>
/// OpponentBomb strategy - damages the opponent's board.
/// Useful when the opponent has opened more than 70% of their board.
/// Not used if the opponent has 1 HP (so as not to finish them off).
/// </summary>
public class OpponentBombStrategy : IBotCardStrategy
{
    public OpponentBombStrategy(
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

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.OpponentBomb];

    public float Evaluate(CardType type)
    {
        var opponent = _context.Opponent;

        if (FindTargetOnOpponentBoard() == new Position(-1, -1))
            return 0f;

        var opponentTotalCells = opponent.Board.Cells.Count;

        if (opponentTotalCells == 0)
            return 0f;

        // Finishing off the opponent is the best use of the card, not a reason to forbid it.
        if (opponent.Health.Current <= 1)
            return 9f;

        var opponentOpenCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);
        var openRatio = (float)opponentOpenCount / opponentTotalCells;

        // The more of the field is open, the higher the chance to hit a mine and take HP.
        return 3f + openRatio * 5f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var position = FindTargetOnOpponentBoard();

        if (position == new Position(-1, -1))
            return false;

        var bot = _context.Bot;

        var payload = new CardUsePayload.OpponentBomb
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }

    /// <summary>
    /// Find a random taken cell on the OPPONENT's board.
    /// </summary>
    private Position FindTargetOnOpponentBoard()
    {
        var board = _context.Opponent.Board;

        if (board.Cells.Count == 0)
            return new Position(-1, -1);

        var takenCells = board.Cells.Values
                              .Where(c => c.IsTaken())
                              .ToList();

        if (takenCells.Count == 0)
            return new Position(-1, -1);

        return takenCells[Random.Shared.Next(takenCells.Count)].Position;
    }
}