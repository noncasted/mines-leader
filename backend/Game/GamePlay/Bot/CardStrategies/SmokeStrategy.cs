using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Дыма (Smoke) - скрывает клетку противника.
/// Рандомный приоритет.
/// </summary>
public class SmokeStrategy : IBotCardStrategy
{
    public SmokeStrategy(
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

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Smoke, CardType.Smoke_Max];

    public float Evaluate(CardType type)
    {
        var opponent = _context.Opponent;
        var opponentTotal = opponent.Board.Cells.Count;

        if (opponentTotal == 0)
            return 0f;

        // Smoke is a disruption card — it does not help the bot win by flagging.
        // Keep priority low so mana and moves are spent on proactive cards instead.
        return 1.5f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var position = _boardUtils.FindRandomTakenPosition(opponent: true);

        if (position == new Position(-1, -1))
            return false;

        var bot = _context.Bot;

        var payload = new CardUsePayload.Smoke
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}