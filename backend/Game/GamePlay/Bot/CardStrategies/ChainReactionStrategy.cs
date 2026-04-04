using Shared;

namespace Game.GamePlay;

public class ChainReactionStrategy : IBotCardStrategy
{
    public ChainReactionStrategy(IBotContext context, BotBoardUtils boardUtils, IBotCommandUtils commandUtils)
    {
        _context = context;
        _boardUtils = boardUtils;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly BotBoardUtils _boardUtils;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.ChainReaction];

    public float Evaluate(CardType type)
    {
        var opponent = _context.Opponent;
        var takenCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken);

        if (takenCount > 5)
            return 5f;

        return 2f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var position = _boardUtils.FindRandomTakenPosition(opponent: true);

        if (position == new Position(-1, -1))
            return false;

        var bot = _context.Bot;

        var payload = new CardUsePayload.ChainReaction
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}