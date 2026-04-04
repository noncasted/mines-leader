using Game.Session;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия ЗипЗапа (ZipZap) - целевое уничтожение конкретной мины на нашей доске.
/// Высокий приоритет если закрыто больше 50% поля.
/// </summary>
public class ZipZapStrategy : IBotCardStrategy
{
    public ZipZapStrategy(
        IBotContext context,
        BotBoardUtils boardUtils,
        IBotCommandUtils commandUtils,
        ISessionEntities sessionEntities)
    {
        _context = context;
        _boardUtils = boardUtils;
        _commandUtils = commandUtils;
        _sessionEntities = sessionEntities;
    }

    private readonly IBotContext _context;
    private readonly BotBoardUtils _boardUtils;
    private readonly IBotCommandUtils _commandUtils;
    private readonly ISessionEntities _sessionEntities;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.ZipZap, CardType.ZipZap_Max];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var totalCells = bot.Board.Cells.Count;
        var closedCells = bot.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken);

        // Если закрыто больше 50% поля - высокий приоритет
        if (closedCells > totalCells * 0.5)
            return 8f;

        return 2f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var position = GetPosition();

        if (position == new Position(-1, -1))
            return false;

        var bot = _context.Bot;

        var payload = new CardUsePayload.ZipZap
        {
            Position = position,
            Type = cardType,
            CardId = cardId
        };

        return _commandUtils.UseCard(bot, cardId, payload);

        Position GetPosition()
        {
            var board = _context.Bot.Board;

            foreach (var (checkPosition, cell) in board.Cells)
            {
                if (cell.IsTaken() == true)
                    continue;

                if (cell.AsFree().MinesAround == 0)
                    continue;

                var neighbours = board.NeighbourPositions(checkPosition);

                foreach (var neighbour in neighbours)
                {
                    if (board.Cells[neighbour].IsTaken() == false)
                        continue;

                    var takenCell = board.Cells[neighbour].AsTaken();

                    if (takenCell.IsFlagged == true)
                        continue;

                    if (takenCell.HasMine == true)
                        return checkPosition;
                }
            }

            return new Position(-1, -1);
        }
    }
}