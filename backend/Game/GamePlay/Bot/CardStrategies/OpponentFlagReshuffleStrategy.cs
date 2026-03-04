using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Перетасовки Флагов Противника (OpponentFlagReshuffle) - перемешивает флаги.
/// Высокий приоритет если противник установил ~50% флагов от количества мин.
/// </summary>
public class OpponentFlagReshuffleStrategy : IBotCardStrategy
{
    public OpponentFlagReshuffleStrategy(
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

    public IReadOnlyList<CardType> TargetCards { get; } =
        [CardType.OpponentFlagReshuffle, CardType.OpponentFlagReshuffle_Max];

    public float Evaluate(CardType type)
    {
        var opponent = _context.Opponent;

        // Полезна только если у противника есть флаги
        if (!_boardUtils.HasFlaggedCells(opponent: true))
            return 0f;

        var flaggedCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken && c.AsTaken().IsFlagged
        );

        var minesCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken && c.AsTaken().HasMine);

        if (minesCount == 0)
            return 0f;

        // Если противник установил примерно 50% флагов от мин
        var flagRatio = (float)flaggedCount / minesCount;

        if (Math.Abs(flagRatio - 0.5f) < 0.3f) // 20-80% флагов
            return 8f; // Высокий приоритет - нарушим его стратегию

        return 3f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        if (!_boardUtils.HasFlaggedCells(opponent: true))
            return false;

        var bot = _context.Bot;
        var position = _boardUtils.FindRandomFlaggedPosition(opponent: true);

        var payload = new CardUsePayload.OpponentFlagReshuffle
        {
            Position = position,
            Type = cardType
        };
        
        return _commandUtils.UseCard(bot, cardId, payload);
    }
}