using Shared;

namespace Game.GamePlay;

public class Medic : ICard<CardUsePayload.Medic>
{
    public CardUseResult Use(IPlayer invoker, CardUsePayload.Medic payload)
    {
        invoker.Health.Heal(1);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Medic()
            {
                TargetPlayer = invoker.User.Id
            }
        };
    }
}