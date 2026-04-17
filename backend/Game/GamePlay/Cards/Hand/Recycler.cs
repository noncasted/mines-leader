using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Discards a chosen card from hand to the stash, then draws cards from the deck.
/// </summary>
public class Recycler : ICard<CardUsePayload.Recycler>
{
    public Recycler(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(CardUseContext context, CardUsePayload.Recycler payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.Recycler_Normal;

        var discardCard = invoker.Hand.Entries.FirstOrDefault(c => c.Id == payload.DiscardCardId);

        if (discardCard == null)
        {
            var candidates = invoker.Hand.Entries
                                    .Where(c => c.Id != context.CardId)
                                    .ToList();

            if (candidates.Count > 0)
                discardCard = candidates[^1];
        }

        var stashChanged = false;
        var deckChanged = false;
        var removedCardId = (Guid?)null;

        if (discardCard != null)
        {
            invoker.Hand.Remove(discardCard.Id);
            invoker.Stash.Add(discardCard.Type);
            stashChanged = true;
            removedCardId = discardCard.Id;
        }

        var addedCards = new List<(Guid Id, CardType Type)>();

        for (var i = 0; i < config.DrawCount; i++)
        {
            if (invoker.Deck.Count == 0)
                break;

            var card = invoker.Deck.DrawCard();
            deckChanged = true;
            var activeCard = invoker.Hand.Add(card);
            addedCards.Add((activeCard.Id, activeCard.Type));
        }

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Recycler()
        {
            TargetPlayer = invoker.User.Id
        });

        if (removedCardId.HasValue == true)
            snapshot.RecordCardRemove(invoker.User.Id, removedCardId.Value);

        foreach (var added in addedCards)
            snapshot.RecordCardAdd(invoker.User.Id, added.Id, added.Type);

        if (deckChanged == true)
            snapshot.RecordDeckUpdate(invoker);

        if (stashChanged == true)
            snapshot.RecordStashUpdate(invoker);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}