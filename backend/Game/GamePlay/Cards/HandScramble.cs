using Shared;

namespace Game.GamePlay;

public class HandScramble : ICard
{
    public HandScramble(IPlayer opponent, MoveSnapshot snapshot)
    {
        _opponent = opponent;
        _snapshot = snapshot;
    }

    private readonly IPlayer _opponent;
    private readonly MoveSnapshot _snapshot;

    public CardUseResult Use()
    {
        var handEntries = _opponent.Hand.Entries.ToList();

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
            _opponent.Hand.Remove(entry.Id);
            _opponent.Deck.AddCard(entry.Type);
            _snapshot.RecordCardRemove(_opponent.User.Id, entry.Id);
        }

        _opponent.Deck.Shuffle();

        for (var i = 0; i < handEntries.Count; i++)
        {
            if (_opponent.Deck.Count == 0)
                break;

            var card = _opponent.Deck.DrawCard();
            var activeCard = _opponent.Hand.Add(card);
            _snapshot.RecordCardAdd(_opponent.User.Id, activeCard.Id, activeCard.Type);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.HandScramble()
            {
                TargetPlayer = _opponent.User.Id
            }
        };
    }
}