using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Peeks the top cards from the deck; the player keeps one chosen card and the rest return to the deck.
/// </summary>
public class Salvage : ICard<CardUsePayload.Salvage>
{
    public Salvage(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(CardUseContext context, CardUsePayload.Salvage payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;

        if (invoker.Deck.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Deck is empty")
            };
        }

        var config = _configs.Value.Salvage_Normal;
        var peekCount = Math.Min(config.PeekCount, invoker.Deck.Count);
        var peeked = new List<CardType>(peekCount);

        for (var i = 0; i < peekCount; i++)
        {
            peeked.Add(invoker.Deck.Peek(i));
        }

        var chosenIndex = Math.Clamp(payload.ChosenIndex, 0, peeked.Count - 1);

        var chosenCard = peeked[chosenIndex];
        invoker.Deck.RemoveCard(chosenCard);
        var activeCard = invoker.Hand.Add(chosenCard);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Salvage()
        {
            TargetPlayer = invoker.User.Id,
            PeekedCards = peeked,
            ChosenIndex = chosenIndex
        });

        snapshot.RecordCardAdd(invoker.User.Id, activeCard.Id, activeCard.Type);
        snapshot.RecordDeckUpdate(invoker);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}