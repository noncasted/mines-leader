using Shared;

namespace Game.GamePlay;

/// <summary>
/// Useless card injected by SabotageDeck. Always fails when played and cannot produce any effect.
/// </summary>
public class Dud : ICard<CardUsePayload.Dud>
{
    public CardUseResult Use(IPlayer invoker, CardUsePayload.Dud payload)
    {
        return new CardUseResult
        {
            Result = EmptyResponse.Fail("Dud card cannot be used"),
            ActionData = new CardActionSnapshot.Dud()
            {
                TargetPlayer = invoker.User.Id
            }
        };
    }
}
