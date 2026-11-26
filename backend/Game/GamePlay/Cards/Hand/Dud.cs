using Shared;

namespace Game.GamePlay;

/// <summary>
/// Useless card injected by SabotageDeck. Always fails when played and cannot produce any effect.
/// </summary>
public class Dud : ICard<CardUsePayload.Dud>
{
    public CardUseResult Use(CardUseContext context, CardUsePayload.Dud payload)
    {
        var invoker = context.Invoker;

        context.Snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Dud()
        {
            TargetPlayer = invoker.User.Id
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Fail("Dud card cannot be used")
        };
    }
}