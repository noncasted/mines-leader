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

        if (lastCard.Value == CardType.MirrorMatch)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Cannot copy Mirror Match")
            };
        }

        var copiedType = lastCard.Value;
        var prefix = context.Snapshot.Count;
        var copiedUse = _serviceProvider.Use(context, lastPayload);

        if (copiedUse.Result.HasError == true)
            return copiedUse;

        // Inner card records CardUse against this card id. Fold it into MirrorMatch so the
        // client receives one CardUse whose payload type matches the card in hand.
        var copiedAction = context.Snapshot.TakeCardUseFrom(prefix);

        context.Snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.MirrorMatch()
        {
            TargetPlayer = invoker.User.Id,
            CopiedCard = copiedType,
            CopiedAction = copiedAction
        });

        return new CardUseResult
        {
            Result = copiedUse.Result
        };
    }
}