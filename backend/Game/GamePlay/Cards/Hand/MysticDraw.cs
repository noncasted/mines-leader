using Shared;

namespace Game.GamePlay;

/// <summary>
/// Flips a coin: heads draws cards from the deck, tails returns random cards from hand back to the deck.
/// </summary>
public class MysticDraw : ICard {
    public MysticDraw(IPlayer owner, MoveSnapshot snapshot, CardConfigOptions.MysticDraw config, IGameRandom gameRandom) {
        _owner = owner;
        _snapshot = snapshot;
        _config = config;
        _gameRandom = gameRandom;
    }

    private readonly IPlayer _owner;
    private readonly MoveSnapshot _snapshot;
    private readonly CardConfigOptions.MysticDraw _config;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use() {
        var isHeads = _gameRandom.FlipCoin(_owner);

        if (isHeads) {
            for (var i = 0; i < _config.WinDraw; i++) {
                if (_owner.Deck.Count == 0)
                    break;

                var card = _owner.Deck.DrawCard();
                var activeCard = _owner.Hand.Add(card);
                _snapshot.RecordCardAdd(_owner.User.Id, activeCard.Id, activeCard.Type);
            }
        } else {
            var toReturn = Math.Min(_config.LoseReturn, _owner.Hand.Entries.Count);
            for (var i = 0; i < toReturn; i++) {
                var index = _gameRandom.Index(_owner, _owner.Hand.Entries.Count);
                var entry = _owner.Hand.Entries[index];
                _owner.Hand.Remove(entry.Id);
                _owner.Deck.AddCard(entry.Type);
                _snapshot.RecordCardRemove(_owner.User.Id, entry.Id);
            }
        }

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.MysticDraw() {
                TargetPlayer = _owner.User.Id,
                IsHeads = isHeads
            }
        };
    }
}
