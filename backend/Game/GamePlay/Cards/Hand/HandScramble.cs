using Shared;

namespace Game.GamePlay;

public class HandScramble : ICard<CardUsePayload.HandScramble>
{
    public HandScramble(IGameContext gameContext, IMoveSnapshotAccessor snapshotAccessor)
    {
        _gameContext = gameContext;
        _snapshotAccessor = snapshotAccessor;
    }

    private readonly IGameContext _gameContext;
    private readonly IMoveSnapshotAccessor _snapshotAccessor;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.HandScramble payload)
    {
        var opponent = _gameContext.GetOpponent(invoker);
        var handEntries = opponent.Hand.Entries.ToList();

        if (handEntries.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Opponent has no cards in hand"),
                ActionData = null
            };
        }

        foreach (var entry in handEntries)
        {
            opponent.Hand.Remove(entry.Id);
            opponent.Deck.AddCard(entry.Type);
            _snapshotAccessor.Snapshot.RecordCardRemove(opponent.User.Id, entry.Id);
        }

        opponent.Deck.Shuffle();

        for (var i = 0; i < handEntries.Count; i++)
        {
            if (opponent.Deck.Count == 0)
                break;

            var card = opponent.Deck.DrawCard();
            var activeCard = opponent.Hand.Add(card);
            _snapshotAccessor.Snapshot.RecordCardAdd(opponent.User.Id, activeCard.Id, activeCard.Type);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.HandScramble()
            {
                TargetPlayer = opponent.User.Id
            }
        };
    }
}