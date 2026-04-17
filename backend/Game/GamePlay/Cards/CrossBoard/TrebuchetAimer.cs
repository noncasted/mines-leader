using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class TrebuchetAimer : ICard<CardUsePayload.TrebuchetAimer>
{
    public TrebuchetAimer(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(CardUseContext context, CardUsePayload.TrebuchetAimer payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.TrebuchetAimer_Normal;

        invoker.Modifiers.Inc(snapshot, PlayerModifier.TrebuchetBoost, config.Size);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.TrebuchetAimer());

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}