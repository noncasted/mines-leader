using Shared;

namespace Game.GamePlay;

public class Scavenger : ICard
{
    public Scavenger(IPlayer owner, MoveSnapshot snapshot, CardConfigOptions.Scavenger config)
    {
        _owner = owner;
        _snapshot = snapshot;
        _config = config;
    }

    private readonly IPlayer _owner;
    private readonly MoveSnapshot _snapshot;
    private readonly CardConfigOptions.Scavenger _config;

    public CardUseResult Use()
    {
        var drawCount = _config.DrawCount;

        for (var i = 0; i < drawCount; i++)
        {
            if (_owner.Deck.Count == 0)
                break;

            var card = _owner.Deck.DrawCard();
            var activeCard = _owner.Hand.Add(card);
            _snapshot.RecordCardAdd(_owner.User.Id, activeCard.Id, activeCard.Type);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Scavenger()
            {
                TargetPlayer = _owner.User.Id
            }
        };
    }
}