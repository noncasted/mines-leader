using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Reduces the mana cost of all cards this turn via the AllCardsDiscount modifier, removed at end of round.
/// </summary>
public class PowerSurge : ICard<CardUsePayload.PowerSurge>
{
    public PowerSurge(ICardConfigs configs, IRoundActionService roundActionService)
    {
        _configs = configs;
        _roundActionService = roundActionService;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use(CardUseContext context, CardUsePayload.PowerSurge payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.PowerSurge_Normal;

        var source = new DurationModifierSource(PlayerModifier.AllCardsDiscount, config.Discount, "powersurge", 1);
        invoker.Modifiers.Add(snapshot, source);

        _roundActionService.Schedule(new ModifierRoundAction(invoker, source));

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.PowerSurge()
        {
            TargetPlayer = invoker.User.Id
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}