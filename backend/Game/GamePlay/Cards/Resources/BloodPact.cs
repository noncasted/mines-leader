using Cluster.Configs;
using Shared;

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
        var manaSource = new DurationModifierSource(PlayerModifier.AdditionalMana, manaGain, "blood_pact", 1);
        invoker.Modifiers.Add(snapshot, manaSource);
        invoker.Mana.SetCurrent(snapshot, invoker.Mana.Current + manaGain);

        var movesSource = new DurationModifierSource(PlayerModifier.AdditionalMoves, config.ExtraMoves, "blood_pact", 1);
        invoker.Modifiers.Add(snapshot, movesSource);

        _roundActionService.Schedule(new ModifierRoundAction(invoker, manaSource));
        _roundActionService.Schedule(new ModifierRoundAction(invoker, movesSource));

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
