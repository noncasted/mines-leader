using Shared;

namespace Game.GamePlay;

public class MineClusterStrategy : IBotCardStrategy {
    public MineClusterStrategy(IBotContext context, BotBoardUtils boardUtils, IBotCommandUtils commandUtils) {
        _context = context;
        _boardUtils = boardUtils;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly BotBoardUtils _boardUtils;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.MineCluster, CardType.MineCluster_Max];

    public float Evaluate(CardType type) {
        var opponent = _context.Opponent;
        var board = opponent.Board;
        var freeCells = board.Cells.Values.Count(c => c.Status == CellStatus.Free);
        return freeCells > 50 ? 6f : 3f;
    }

    public bool Execute(Guid cardId, CardType cardType) {
        var position = _boardUtils.FindRandomTakenPosition(opponent: true);
        if (position == new Position(-1, -1)) return false;

        var payload = new CardUsePayload.MineCluster { Type = cardType, Position = position };
        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}
