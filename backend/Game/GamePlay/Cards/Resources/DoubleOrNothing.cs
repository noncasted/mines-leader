using Shared;

namespace Game.GamePlay;

/// <summary>
/// Flips a coin: heads doubles current mana, tails sets mana to zero.
/// </summary>
public class DoubleOrNothing : ICard {
    public DoubleOrNothing(IPlayer owner, IGameRandom gameRandom) {
        _owner = owner;
        _gameRandom = gameRandom;
    }

    private readonly IPlayer _owner;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use() {
        var isHeads = _gameRandom.FlipCoin(_owner);
        int resultMana;

        if (isHeads) {
            resultMana = _owner.Mana.Current * 2;
            var bonus = _owner.Mana.Current;
            _owner.Modifiers.Set(PlayerModifier.AdditionalMana,
                _owner.Modifiers.Get(PlayerModifier.AdditionalMana) + bonus);
            _owner.Mana.SetCurrent(resultMana);
        } else {
            resultMana = 0;
            _owner.Mana.SetCurrent(0);
        }

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.DoubleOrNothing() {
                TargetPlayer = _owner.User.Id,
                IsHeads = isHeads,
                ResultMana = resultMana
            }
        };
    }
}
