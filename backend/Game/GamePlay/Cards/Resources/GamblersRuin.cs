using Shared;

namespace Game.GamePlay;

/// <summary>
/// Flips a coin: heads draws cards and grants temporary mana, tails discards random cards from hand.
/// </summary>
public class GamblersRuin : ICard {
    public GamblersRuin(IPlayer owner, MoveSnapshot snapshot, CardConfigOptions.GamblersRuin config, IRoundActionService roundActionService, IGameRandom gameRandom) {
        _owner = owner;
        _snapshot = snapshot;
        _config = config;
        _roundActionService = roundActionService;
        _gameRandom = gameRandom;
    }

    private readonly IPlayer _owner;
    private readonly MoveSnapshot _snapshot;
    private readonly CardConfigOptions.GamblersRuin _config;
    private readonly IRoundActionService _roundActionService;
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

            var manaGain = _config.WinMana;
            _owner.Modifiers.Set(PlayerModifier.AdditionalMana,
                _owner.Modifiers.Get(PlayerModifier.AdditionalMana) + manaGain);
            _owner.Mana.SetCurrent(_owner.Mana.Current + manaGain);

            var disposeAction = new GamblersRuinDisposeAction(_owner, manaGain);
            _roundActionService.Schedule(disposeAction, 1);
        } else {
            var toDiscard = Math.Min(_config.LoseDiscard, _owner.Hand.Entries.Count);
            for (var i = 0; i < toDiscard; i++) {
                var index = _gameRandom.Index(_owner, _owner.Hand.Entries.Count);
                var entry = _owner.Hand.Entries[index];
                _owner.Hand.Remove(entry.Id);
                _owner.Stash.Add(entry.Type);
                _snapshot.RecordCardRemove(_owner.User.Id, entry.Id);
            }
        }

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.GamblersRuin() {
                TargetPlayer = _owner.User.Id,
                IsHeads = isHeads
            }
        };
    }
}

public class GamblersRuinDisposeAction : IRoundAction {
    public GamblersRuinDisposeAction(IPlayer owner, int manaGain) {
        _owner = owner;
        _manaGain = manaGain;
    }

    private readonly IPlayer _owner;
    private readonly int _manaGain;

    public void Execute() {
        var current = _owner.Modifiers.Get(PlayerModifier.AdditionalMana);
        _owner.Modifiers.Set(PlayerModifier.AdditionalMana, Math.Max(0, current - _manaGain));
    }
}
