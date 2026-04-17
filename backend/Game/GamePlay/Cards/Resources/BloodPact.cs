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

    public CardUseResult Use(CardUseContext context, CardUsePayload.BloodPact payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.BloodPact_Normal;

        invoker.Health.TakeDamage(snapshot, config.HpCost);

        var manaGain = config.ManaGain;
        invoker.Modifiers.Inc(snapshot, PlayerModifier.AdditionalMana, manaGain);
        invoker.Mana.SetCurrent(snapshot, invoker.Mana.Current + manaGain);

        invoker.Modifiers.Inc(snapshot, PlayerModifier.AdditionalMoves, config.ExtraMoves);

        _roundActionService.Schedule(new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMana, manaGain), 1);

        _roundActionService.Schedule(
            new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMoves, config.ExtraMoves), 1);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.BloodPact()
        {
            TargetPlayer = invoker.User.Id
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}