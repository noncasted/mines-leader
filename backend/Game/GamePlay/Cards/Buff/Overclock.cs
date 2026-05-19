using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Grants extra moves this turn via the AdditionalMoves modifier.
/// </summary>
public class Overclock : ICard<CardUsePayload.Overclock>
{
    public Overclock(ICardConfigs configs, IRoundActionService roundActionService)
    {
        _configs = configs;
        _roundActionService = roundActionService;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use(CardUseContext context, CardUsePayload.Overclock payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.Overclock_Normal;

        var source = new DurationModifierSource(PlayerModifier.AdditionalMoves, config.ExtraMoves, "overclock", 1);
        invoker.Modifiers.Add(snapshot, source);

        _roundActionService.Schedule(new ModifierRoundAction(invoker, source));

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Overclock()
        {
            TargetPlayer = invoker.User.Id
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}