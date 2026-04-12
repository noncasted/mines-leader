using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Sacrifices HP to gain temporary mana and extra moves this turn.
/// </summary>
public class BloodPact : ICard<CardUsePayload.BloodPact>
{
    public BloodPact(ICardConfigs configs, IRoundActionService roundActionService)
    {
        _configs = configs;
        _roundActionService = roundActionService;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.BloodPact payload)
    {
        var config = _configs.Value.BloodPact_Normal;

        invoker.Health.TakeDamage(config.HpCost);

        var manaGain = config.ManaGain;
        invoker.Modifiers.Inc(PlayerModifier.AdditionalMana, manaGain);
        invoker.Mana.SetCurrent(invoker.Mana.Current + manaGain);

        invoker.Modifiers.Inc(PlayerModifier.AdditionalMoves, config.ExtraMoves);

        _roundActionService.Schedule(new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMana, manaGain), 1);

        _roundActionService.Schedule(
            new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMoves, config.ExtraMoves), 1);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.BloodPact()
            {
                TargetPlayer = invoker.User.Id
            }
        };
    }
}