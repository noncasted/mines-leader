using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class Scavenger : ICard<CardUsePayload.Scavenger>
{
    public Scavenger(ICardConfigs configs, IMoveSnapshotAccessor snapshotAccessor)
    {
        _configs = configs;
        _snapshotAccessor = snapshotAccessor;
    }

    private readonly ICardConfigs _configs;
    private readonly IMoveSnapshotAccessor _snapshotAccessor;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Scavenger payload)
    {
        var drawCount = _configs.Value.Scavenger_Normal.DrawCount;

        for (var i = 0; i < drawCount; i++)
        {
            if (invoker.Deck.Count == 0)
                break;

            var card = invoker.Deck.DrawCard();
            var activeCard = invoker.Hand.Add(card);
            _snapshotAccessor.Snapshot.RecordCardAdd(invoker.User.Id, activeCard.Id, activeCard.Type);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Scavenger()
            {
                TargetPlayer = invoker.User.Id
            }
        };
    }
}
