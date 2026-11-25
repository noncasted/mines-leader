using Shared;

namespace Game.GamePlay;

/// <summary>
/// TrebuchetAimer strategy - prepares an empowered attack.
/// High priority if we have a Trebuchet and the opponent has opened more cells than we have.
/// Spends no moves, so the priority is high whenever all conditions hold.
/// </summary>
public class TrebuchetAimerStrategy : IBotCardStrategy
{
    public TrebuchetAimerStrategy(
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

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.TrebuchetAimer, CardType.TrebuchetAimer_Max];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var opponent = _context.Opponent;

        var handCards = bot.Hand.Entries;
        var stashCards = bot.Stash.Collect();

        var hasTrebuchet = handCards.Any(c => c.Type == CardType.Trebuchet || c.Type == CardType.Trebuchet_Max) ||
                           stashCards.Contains(CardType.Trebuchet) ||
                           stashCards.Contains(CardType.Trebuchet_Max);

        if (!hasTrebuchet)
            return 0f;

        // If the Trebuchet already has a bonus, another one is not needed
        if (bot.Modifiers.Values[PlayerModifier.TrebuchetBoost] > 0)
            return 1f; // Low priority, the bonus is already there

        var botOpenCount = bot.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);
        var opponentOpenCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);

        if (opponentOpenCount > botOpenCount)
            return 8f; // High priority - the opponent is ahead

        return 2f; // Low priority - we are ahead
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;

        var payload = new CardUsePayload.TrebuchetAimer()
        {
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}