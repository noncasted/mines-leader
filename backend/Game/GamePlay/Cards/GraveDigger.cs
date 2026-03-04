using Shared;

namespace Game.GamePlay;

public class GraveDigger : ICard
{
    public GraveDigger(
        IPlayer owner,
        MoveSnapshot snapshot)
    {
        _owner = owner;
        _snapshot = snapshot;
    }

    private readonly IPlayer _owner;
    private readonly MoveSnapshot _snapshot;

    public CardUseResult Use()
    {
        if (_owner.Stash.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No cards in stash"),
                ActionData = null
            };
        }

        var card = _owner.Stash.Pick();

        var activeCard = _owner.Hand.Add(card);
        _snapshot.RecordCardAdd(_owner.User.Id, activeCard.Id, activeCard.Type);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Gravedigger()
            {
                TargetPlayer = _owner.User.Id
            }
        };
    }
}