using Shared;

namespace Game.GamePlay;

/// <summary>
/// Discards a chosen card from hand to the stash, then draws cards from the deck.
/// </summary>
public class Recycler : ICard {
    public Recycler(IPlayer owner, MoveSnapshot snapshot, CardConfigOptions.Recycler config, CardUsePayload.Recycler payload) {
        _owner = owner;
        _snapshot = snapshot;
        _config = config;
        _payload = payload;
    }

    private readonly IPlayer _owner;
    private readonly MoveSnapshot _snapshot;
    private readonly CardConfigOptions.Recycler _config;
    private readonly CardUsePayload.Recycler _payload;

    public CardUseResult Use() {
        var discardCard = _owner.Hand.Entries.FirstOrDefault(c => c.Id == _payload.DiscardCardId);
        if (discardCard != null) {
            _owner.Hand.Remove(discardCard.Id);
            _owner.Stash.Add(discardCard.Type);
            _snapshot.RecordCardRemove(_owner.User.Id, discardCard.Id);
        }

        for (var i = 0; i < _config.DrawCount; i++) {
            if (_owner.Deck.Count == 0)
                break;

            var card = _owner.Deck.DrawCard();
            var activeCard = _owner.Hand.Add(card);
            _snapshot.RecordCardAdd(_owner.User.Id, activeCard.Id, activeCard.Type);
        }

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Recycler() {
                TargetPlayer = _owner.User.Id
            }
        };
    }
}
