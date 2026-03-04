using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Бомбы Противника (OpponentBomb) - наносит урон доске противника.
/// Полезна когда противник открыл больше 70% своей доски.
/// </summary>
public class OpponentBombStrategy : IBotCardStrategy
{
    public OpponentBombStrategy(
        IBotContext context,
        ICardFactory cardFactory,
        BotBoardUtils boardUtils,
        IBotCommandUtils commandUtils)
    {
        _context = context;
        _cardFactory = cardFactory;
        _boardUtils = boardUtils;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly ICardFactory _cardFactory;
    private readonly BotBoardUtils _boardUtils;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.OpponentBomb];

    public float Evaluate(CardType type)
    {
        var opponent = _context.Opponent;
        var opponentTotalCells = opponent.Board.Cells.Count;
        var opponentOpenCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);

        // Если противник открыл более 70% поля - наносим урон
        if (opponentOpenCount > opponentTotalCells * 0.7)
            return 7f;

        return 1f; // Низкий приоритет в остальных случаях
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var position = GetPosition();

        if (position == new Position(-1, -1))
            return false;

        var bot = _context.Bot;
        
        var payload = new CardUsePayload.OpponentBomb
        {
            Position = position,
            Type = cardType
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
