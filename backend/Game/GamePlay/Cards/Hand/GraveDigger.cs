using Shared;

namespace Game.GamePlay;

public class GraveDigger : ICard<CardUsePayload.Gravedigger>
{
    public CardUseResult Use(CardUseContext context, CardUsePayload.Gravedigger payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;

        if (invoker.Stash.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No cards in stash")
            };
        }

        var card = invoker.Stash.Pick();

        var activeCard = invoker.Hand.Add(card);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Gravedigger()
        {
            TargetPlayer = invoker.User.Id
        });

        snapshot.RecordCardAdd(invoker.User.Id, activeCard.Id, activeCard.Type);
        snapshot.RecordStashUpdate(invoker);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}