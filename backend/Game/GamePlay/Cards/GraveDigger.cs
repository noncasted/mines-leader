using Shared;

namespace Game.GamePlay;

public class GraveDigger : ICard
{
    public GraveDigger(
        IPlayer owner,
        MoveSnapshot snapshot,
        ICardFactory cardFactory)
    {
        _owner = owner;
        _snapshot = snapshot;
        _cardFactory = cardFactory;
    }

    private readonly IPlayer _owner;
    private readonly MoveSnapshot _snapshot;
    private readonly ICardFactory _cardFactory;

    public EmptyResponse Use()
    {
        if (_owner.Stash.Count == 0)
            return EmptyResponse.Fail("No cards in stash");

        var card = _owner.Stash.Pick();

        _owner.Hand.Add(card);
        _cardFactory.CreateEntity(_owner, card);

        return EmptyResponse.Ok;
    }
}