using Shared;

namespace Game.GamePlay;

/// <summary>
/// Steals a random card from the opponent's hand and adds it to the owner's hand.
/// </summary>
public class CardThief : ICard
{
    public CardThief(
        IPlayer owner,
        IPlayer opponent,
        MoveSnapshot snapshot,
        IGameRandom gameRandom)
    {
        _owner = owner;
        _opponent = opponent;
        _snapshot = snapshot;
        _gameRandom = gameRandom;
    }

    private readonly IPlayer _owner;
    private readonly IPlayer _opponent;
    private readonly MoveSnapshot _snapshot;
    private readonly IGameRandom _gameRandom;

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

        var index = _gameRandom.Index(_owner, handEntries.Count);
        var entry = handEntries[index];

        _opponent.Hand.Remove(entry.Id);
        _snapshot.RecordCardRemove(_opponent.User.Id, entry.Id);

        var activeCard = _owner.Hand.Add(entry.Type);
        _snapshot.RecordCardAdd(_owner.User.Id, activeCard.Id, activeCard.Type);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.CardThief()
            {
                TargetPlayer = _opponent.User.Id,
                StolenCard = entry.Type
            }
        };
    }
}
