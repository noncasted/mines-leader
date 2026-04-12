using Shared;

namespace Game.GamePlay;

public class GraveDigger : ICard<CardUsePayload.Gravedigger>
{
    public GraveDigger(IMoveSnapshotAccessor snapshotAccessor)
    {
        _snapshotAccessor = snapshotAccessor;
    }

    private readonly IMoveSnapshotAccessor _snapshotAccessor;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Gravedigger payload)
    {
        if (invoker.Stash.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No cards in stash"),
                ActionData = null
            };
        }

        var card = invoker.Stash.Pick();

        var activeCard = invoker.Hand.Add(card);
        _snapshotAccessor.Snapshot.RecordCardAdd(invoker.User.Id, activeCard.Id, activeCard.Type);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Gravedigger()
            {
                TargetPlayer = invoker.User.Id
            }
        };
    }
}