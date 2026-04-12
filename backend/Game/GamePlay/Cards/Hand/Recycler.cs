using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Discards a chosen card from hand to the stash, then draws cards from the deck.
/// </summary>
public class Recycler : ICard<CardUsePayload.Recycler>
{
    public Recycler(ICardConfigs configs, IMoveSnapshotAccessor snapshotAccessor)
    {
        _configs = configs;
        _snapshotAccessor = snapshotAccessor;
    }

    private readonly ICardConfigs _configs;
    private readonly IMoveSnapshotAccessor _snapshotAccessor;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Recycler payload)
    {
        var config = _configs.Value.Recycler_Normal;

        var discardCard = invoker.Hand.Entries.FirstOrDefault(c => c.Id == payload.DiscardCardId);

        if (discardCard == null)
        {
            var candidates = invoker.Hand.Entries
                .Where(c => c.Id != _snapshotAccessor.CardId)
                .ToList();

            if (candidates.Count > 0)
                discardCard = candidates[^1];
        }

        if (discardCard != null)
        {
            invoker.Hand.Remove(discardCard.Id);
            invoker.Stash.Add(discardCard.Type);
            _snapshotAccessor.Snapshot.RecordCardRemove(invoker.User.Id, discardCard.Id);
        }

        for (var i = 0; i < config.DrawCount; i++)
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
            ActionData = new CardActionSnapshot.Recycler()
            {
                TargetPlayer = invoker.User.Id
            }
        };
    }
}