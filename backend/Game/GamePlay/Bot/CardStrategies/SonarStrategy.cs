using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Сонара (Sonar) - автоматически флажит мины в области своего поля.
/// Высокий приоритет когда много закрытых клеток с минами без флагов.
/// </summary>
public class SonarStrategy : IBotCardStrategy
{
    public SonarStrategy(IBotContext context, BotBoardUtils boardUtils, IBotCommandUtils commandUtils)
    {
        _context = context;
        _boardUtils = boardUtils;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly BotBoardUtils _boardUtils;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Sonar];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var unflaggedMines = bot.Board.Cells.Values
            .Count(c => {
                if (c.Status != CellStatus.Taken) return false;
                var taken = c.AsTaken();
                return taken.HasMine && !taken.IsFlagged;
            });

        if (unflaggedMines >= 5)
            return 8f;

        if (unflaggedMines >= 2)
            return 5f;

        if (unflaggedMines > 0)
            return 2f;

        return 0f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var position = _boardUtils.FindRandomTakenPosition(opponent: false);

        if (position == new Position(-1, -1))
            return false;

        var bot = _context.Bot;

        var payload = new CardUsePayload.Sonar
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}
