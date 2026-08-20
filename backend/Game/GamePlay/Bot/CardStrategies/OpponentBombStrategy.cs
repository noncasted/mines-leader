using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Бомбы Противника (OpponentBomb) - наносит урон доске противника.
/// Полезна когда противник открыл больше 70% своей доски.
/// Не используется если у противника 1 HP (чтобы не добивать).
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

        // Добить противника — лучшее применение карты, а не запрет на неё.
        if (opponent.Health.Current <= 1)
            return 9f;

        var opponentOpenCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);
        var openRatio = (float)opponentOpenCount / opponentTotalCells;

        // Чем больше поля вскрыто, тем выше шанс попасть по мине и снять хп.
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