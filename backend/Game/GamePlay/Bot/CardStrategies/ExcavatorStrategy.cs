using Shared;

namespace Game.GamePlay;

public class ExcavatorStrategy : IBotCardStrategy {
    public ExcavatorStrategy(IBotContext context, BotBoardUtils boardUtils, IBotCommandUtils commandUtils) {
        _context = context;
        _boardUtils = boardUtils;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly BotBoardUtils _boardUtils;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Excavator, CardType.Excavator_Max];

    public float Evaluate(CardType type) {
        var board = _context.Bot.Board;
        var totalCells = board.Cells.Count;
        var takenCells = board.Cells.Values.Count(c => c.Status == CellStatus.Taken);
        return takenCells > totalCells * 0.5f ? 8f : 2f;
    }

    public bool Execute(Guid cardId, CardType cardType) {
        var position = _boardUtils.FindRandomTakenPosition(opponent: false);
        if (position == new Position(-1, -1)) return false;

        var payload = new CardUsePayload.Excavator { Type = cardType, Position = position };
        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}
