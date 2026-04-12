using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Grants temporary mana this turn via the AdditionalMana modifier, removed at end of round.
/// </summary>
public class ManaSurge : ICard<CardUsePayload.ManaSurge> {
    public ManaSurge(ICardConfigs configs, IRoundActionService roundActionService) {
        _configs = configs;
        _roundActionService = roundActionService;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.ManaSurge payload) {
        var gain = _configs.Value.ManaSurge_Normal.ManaGain;

        invoker.Modifiers.Inc(PlayerModifier.AdditionalMana, gain);
        invoker.Mana.SetCurrent(invoker.Mana.Current + gain);

        _roundActionService.Schedule(
            new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMana, gain), 1);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ManaSurge() {
                TargetPlayer = invoker.User.Id
            }
        };
    }
}
