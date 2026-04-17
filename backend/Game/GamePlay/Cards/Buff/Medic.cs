using Shared;

namespace Game.GamePlay;

public class Medic : ICard<CardUsePayload.Medic>
{
    public CardUseResult Use(CardUseContext context, CardUsePayload.Medic payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;

        invoker.Health.Heal(snapshot, 1);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Medic()
        {
            TargetPlayer = invoker.User.Id
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}