using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Grants extra moves this turn.
/// </summary>
public class Adrenaline : ICard<CardUsePayload.Adrenaline>
{
    public Adrenaline(ICardConfigs configs, IRoundActionService roundActionService)
    {
        _configs = configs;
        _roundActionService = roundActionService;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use(CardUseContext context, CardUsePayload.Adrenaline payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.Adrenaline_Normal;

        invoker.Modifiers.Inc(snapshot, PlayerModifier.AdditionalMoves, config.ExtraMoves);

        _roundActionService.Schedule(
            new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMoves, config.ExtraMoves), 1);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Adrenaline()
        {
            TargetPlayer = invoker.User.Id
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}