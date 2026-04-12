using Shared;

namespace Game.GamePlay;

/// <summary>
/// Increments the Shield modifier, absorbing the next mine hit and preventing HP damage.
/// </summary>
public class Shield : ICard<CardUsePayload.Shield> {
    public CardUseResult Use(IPlayer invoker, CardUsePayload.Shield payload) {
        invoker.Modifiers.Inc(PlayerModifier.Shield);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Shield() {
                TargetPlayer = invoker.User.Id
            }
        };
    }
}
