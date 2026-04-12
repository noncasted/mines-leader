using Shared;

namespace Game.GamePlay;

public class CarpetBombStrategy : IBotCardStrategy
{
    public CarpetBombStrategy(IBotContext context, BotBoardUtils boardUtils, IBotCommandUtils commandUtils)
    {
        _context = context;
        _boardUtils = boardUtils;
        _commandUtils = commandUtils;
    }

    private readonly IBotContext _context;
    private readonly BotBoardUtils _boardUtils;
    private readonly IBotCommandUtils _commandUtils;

    public IReadOnlyList<CardType> TargetCards { get; } = [CardType.CarpetBomb, CardType.CarpetBomb_Max];

    public float Evaluate(CardType type)
    {
        var bot = _context.Bot;

        if (bot.Mana.Current < 5)
            return 0f;
        return 6f;
    }

    public bool Execute(Guid cardId, CardType cardType)
    {
        var position = _boardUtils.FindRandomTakenPosition(opponent: true);

        if (position == new Position(-1, -1))
            return false;

        var payload = new CardUsePayload.CarpetBomb { Type = cardType, Position = position };
        return _commandUtils.UseCard(_context.Bot, cardId, payload);
    }
}