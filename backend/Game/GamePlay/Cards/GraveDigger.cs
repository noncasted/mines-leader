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

    public EmptyResponse Use()
    {
        if (_owner.Stash.Count == 0)
            return EmptyResponse.Fail("No cards in stash");
        
        var card = _owner.Stash.Pick();
        
        _owner.Hand.Add(card);
        _snapshot.RecordCardTakeoutFromStash(_owner.User.Id, card);

        return EmptyResponse.Ok;
    }
}