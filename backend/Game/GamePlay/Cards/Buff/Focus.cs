using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Applies a one-time mana cost discount to the next card played via the NextCardDiscount modifier.
/// </summary>
public class Focus : ICard<CardUsePayload.Focus> {
    public Focus(ICardConfigs configs, IRoundActionService roundActionService) {
        _configs = configs;
        _roundActionService = roundActionService;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Focus payload) {
        var config = _configs.Value.Focus_Normal;

        invoker.Modifiers.Inc(PlayerModifier.NextCardDiscount, config.Discount);

        _roundActionService.Schedule(
            new ModifierDisposeAction(invoker, PlayerModifier.NextCardDiscount, config.Discount), 1);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Focus() {
                TargetPlayer = invoker.User.Id
            }
        };
    }
}
