using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Тумана Войны (FogOfWar) - скрывает числа на открытых клетках противника.
/// Приоритет зависит от количества открытых клеток у противника.
/// </summary>
public class FogOfWarStrategy : IBotCardStrategy
{
    public FogOfWarStrategy(IBotContext context, BotBoardUtils boardUtils, IBotCommandUtils commandUtils)
    {
        _context = context;
        _boardUtils = boardUtils;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly BotBoardUtils _boardUtils;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.FogOfWar, CardType.FogOfWar_Max];

    public float Evaluate(CardType type)
    {
        var opponent = _context.Opponent;
        var openCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);

        if (openCount > 20)
            return 6f;

        if (openCount > 5)
            return 3f;

        return 1f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var position = _boardUtils.FindRandomFreePosition(opponent: true);

        if (position == new Position(-1, -1))
            return false;

        var bot = _context.Bot;

        var payload = new CardUsePayload.FogOfWar
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}