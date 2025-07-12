using Shared;

namespace Game.GamePlay;

public class GraveDigger : ICard
{
    public GraveDigger(
        IPlayer owner,
        MoveSnapshot snapshot,
        CardUsePayload.Gravedigger payload)
    {
        _owner = owner;
        _snapshot = snapshot;
        _payload = payload;
    }

    private readonly IPlayer _owner;
    private readonly MoveSnapshot _snapshot;
    private readonly CardUsePayload.Gravedigger _payload;

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