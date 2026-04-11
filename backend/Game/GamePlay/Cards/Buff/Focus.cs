using Shared;

namespace Game.GamePlay;

/// <summary>
/// Applies a one-time mana cost discount to the next card played via the NextCardDiscount modifier.
/// </summary>
public class Focus : ICard {
    public Focus(IPlayer owner, CardConfigOptions.Focus config) {
        _owner = owner;
        _config = config;
    }

    private readonly IPlayer _owner;
    private readonly CardConfigOptions.Focus _config;

    public CardUseResult Use() {
        _owner.Modifiers.Set(PlayerModifier.NextCardDiscount,
            _owner.Modifiers.Get(PlayerModifier.NextCardDiscount) + _config.Discount);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Focus() {
                TargetPlayer = _owner.User.Id
            }
        };
    }
}
