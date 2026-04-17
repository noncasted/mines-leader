using Shared;

namespace Game.GamePlay;

/// <summary>
/// Copies the last card played by the opponent and applies its effect for the owner.
/// </summary>
public class MirrorMatch : ICard<CardUsePayload.MirrorMatch>
{
    public MirrorMatch(IGameContext gameContext, IServiceProvider serviceProvider)
    {
        _gameContext = gameContext;
        _serviceProvider = serviceProvider;
    }

    private readonly IGameContext _gameContext;
    private readonly IServiceProvider _serviceProvider;

    public CardUseResult Use(CardUseContext context, CardUsePayload.MirrorMatch payload)
    {
        var invoker = context.Invoker;
        var opponent = _gameContext.GetOpponent(invoker);
        var lastCard = opponent.Actions.LastUsedCardType;
        var lastPayload = opponent.Actions.LastUsedPayload;

        if (lastCard == null || lastPayload == null)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Opponent has not used any card yet")
            };
        }

        var copiedType = lastCard.Value;
        var copiedUse = _serviceProvider.Use(context, lastPayload);

        context.Snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.MirrorMatch()
        {
            TargetPlayer = invoker.User.Id,
            CopiedCard = copiedType
        });

        return new CardUseResult
        {
            Result = copiedUse.Result
        };
    }
}