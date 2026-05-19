using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Grants temporary mana this turn via the AdditionalMana modifier, removed at end of round.
/// </summary>
public class ManaSurge : ICard<CardUsePayload.ManaSurge>
{
    public ManaSurge(ICardConfigs configs, IRoundActionService roundActionService)
    {
        _configs = configs;
        _roundActionService = roundActionService;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use(CardUseContext context, CardUsePayload.ManaSurge payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var gain = _configs.Value.ManaSurge_Normal.ManaGain;

        var source = new DurationModifierSource(PlayerModifier.AdditionalMana, gain, "mana_surge", 1);
        invoker.Modifiers.Add(snapshot, source);
        invoker.Mana.SetCurrent(snapshot, invoker.Mana.Current + gain);

        _roundActionService.Schedule(new ModifierRoundAction(invoker, source));

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.ManaSurge()
        {
            TargetPlayer = invoker.User.Id
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}
