using Shared;

namespace Game.GamePlay;

/// <summary>
/// Increments the Shield modifier, absorbing the next mine hit and preventing HP damage.
/// </summary>
public class Shield : ICard {
    public Shield(IPlayer owner) {
        _owner = owner;
    }

    private readonly IPlayer _owner;

    public CardUseResult Use() {
        _owner.Modifiers.Inc(PlayerModifier.Shield);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Shield() {
                TargetPlayer = _owner.User.Id
            }
        };
    }
}
