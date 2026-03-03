using Common.Reactive;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Стратегия Прицела Требушета (TrebuchetAimer) - подготовка усиленной атаки.
/// Высокий приоритет если у нас есть Требушет и противник открыл больше клеток чем мы.
/// Не тратит ходы, поэтому приоритет при наличии всех условий.
/// </summary>
public class TrebuchetAimerStrategy : IBotCardStrategy
{
    public TrebuchetAimerStrategy(
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

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.TrebuchetAimer, CardType.TrebuchetAimer_Max];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var opponent = _context.Opponent;

        // Проверяем есть ли Требушет в руке или запасах
        var handCards = bot.Hand.Entries;
        var stashCards = bot.Stash.Collect();

        var hasTrebuchet = handCards.Contains(CardType.Trebuchet) ||
                          handCards.Contains(CardType.Trebuchet_Max) ||
                          stashCards.Contains(CardType.Trebuchet) ||
                          stashCards.Contains(CardType.Trebuchet_Max);

        if (!hasTrebuchet)
            return 0f;

        // Если у Требушета уже есть бонус - не нужен еще один
        if (bot.Modifiers.Values[PlayerModifier.TrebuchetBoost] > 0)
            return 1f; // Низкий приоритет, бонус уже есть

        // Проверяем открыли ли противник больше клеток чем мы
        var botOpenCount = bot.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);
        var opponentOpenCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);

        if (opponentOpenCount > botOpenCount)
            return 8f; // Высокий приоритет - противник впереди

        return 2f; // Низкий приоритет - мы впереди
    }

    public bool Execute(CardType cardType)
    {
        var bot = _context.Bot;
        
        var payload = new CardUsePayload.TrebuchetAimer()
        {
            Type = cardType
        };
        
        return _commandUtils.UseCard(bot, payload);
    }
}
