using Shared;

namespace Game.GamePlay;

public class FortuneBlastStrategy : IBotCardStrategy {
    public FortuneBlastStrategy(IBotContext context, BotBoardUtils boardUtils, IBotCommandUtils commandUtils) {
        _context = context;
        _boardUtils = boardUtils;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly BotBoardUtils _boardUtils;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.FortuneBlast];

    public float Evaluate(CardType type) {
        return 5f;
    }

    public bool Execute(Guid cardId, CardType cardType) {
        var position = _boardUtils.FindRandomTakenPosition(opponent: true);
        if (position == new Position(-1, -1)) return false;

        var payload = new CardUsePayload.FortuneBlast { Type = cardType, Position = position };
        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}
