using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class Scavenger : ICard<CardUsePayload.Scavenger>
{
    public Scavenger(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(CardUseContext context, CardUsePayload.Scavenger payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var drawCount = _configs.Value.Scavenger_Normal.DrawCount;

        var drawn = 0;
        var addedCards = new List<(Guid Id, CardType Type)>();

        for (var i = 0; i < drawCount; i++)
        {
            if (invoker.Deck.Count == 0)
                break;

            var card = invoker.Deck.DrawCard();
            var activeCard = invoker.Hand.Add(card);
            addedCards.Add((activeCard.Id, activeCard.Type));
            drawn++;
        }

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Scavenger()
        {
            TargetPlayer = invoker.User.Id
        });

        foreach (var added in addedCards)
            snapshot.RecordCardAdd(invoker.User.Id, added.Id, added.Type);

        if (drawn > 0)
            snapshot.RecordDeckUpdate(invoker);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}