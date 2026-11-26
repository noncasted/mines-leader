using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Trebuchet strategy - long-range attack on the opponent's board.
/// Maximum priority if the TrebuchetAimer modifier is active.
/// High priority if the opponent has opened more cells than we have.
/// </summary>
public class TrebuchetStrategy : IBotCardStrategy
{
    public TrebuchetStrategy(
        IBotContext context,
        ICardConfigs cardConfigs,
        IBotCommandUtils commandUtils)
    {
        _context = context;
        _cardConfigs = cardConfigs;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly ICardConfigs _cardConfigs;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.Trebuchet, CardType.Trebuchet_Max];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;
        var opponent = _context.Opponent;

        var opponentTakenCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Taken);
        var opponentFreeCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);

        if (opponentTakenCount == 0 || opponentFreeCount == 0)
            return 0f;

        if (bot.Modifiers.Values[PlayerModifier.TrebuchetBoost] > 0)
            return 10f;

        var botOpenCount = bot.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);
        var opponentOpenCount = opponent.Board.Cells.Values.Count(c => c.Status == CellStatus.Free);

        if (opponentOpenCount > botOpenCount)
            return 7f; // High priority - the opponent is ahead

        return 2f; // Low priority - we are ahead
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var bot = _context.Bot;

        // The same size the card will compute: the base diamond plus the TrebuchetAimer boost.
        var size = _cardConfigs.Value.Trebuchet_Normal.Size +
                   (int)bot.Modifiers.Values[PlayerModifier.TrebuchetBoost] * 2;

        var position = BotCardTargeting.BestTrebuchetCentre(_context.Opponent.Board, size);

        if (position == BotCardTargeting.None)
            return false;

        var payload = new CardUsePayload.Trebuchet
        {
            Position = position,
            Type = cardType
        };

        return _commandUtils.UseCard(bot, cardId, payload);
    }
}