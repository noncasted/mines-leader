using Shared;

namespace Game.GamePlay;

public class HandScramble : ICard<CardUsePayload.HandScramble>
{
    public HandScramble(IGameContext gameContext)
    {
        _gameContext = gameContext;
    }

    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.HandScramble payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var handEntries = opponent.Hand.Entries.ToList();

        if (handEntries.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Opponent has no cards in hand")
            };
        }

        var removedIds = new List<Guid>();

        foreach (var entry in handEntries)
        {
            opponent.Hand.Remove(entry.Id);
            opponent.Deck.AddCard(entry.Type);
            removedIds.Add(entry.Id);
        }

        opponent.Deck.Shuffle();

        var addedCards = new List<(Guid Id, CardType Type)>();

        for (var i = 0; i < handEntries.Count; i++)
        {
            if (opponent.Deck.Count == 0)
                break;

            var card = opponent.Deck.DrawCard();
            var activeCard = opponent.Hand.Add(card);
            addedCards.Add((activeCard.Id, activeCard.Type));
        }

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.HandScramble()
        {
            TargetPlayer = opponent.User.Id
        });

        foreach (var id in removedIds)
            snapshot.RecordCardRemove(opponent.User.Id, id);

        foreach (var added in addedCards)
            snapshot.RecordCardAdd(opponent.User.Id, added.Id, added.Type);

        snapshot.RecordDeckUpdate(opponent);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}