using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class TrebuchetAimer : ICard<CardUsePayload.TrebuchetAimer> {
    public TrebuchetAimer(ICardConfigs configs) {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.TrebuchetAimer payload) {
        var config = _configs.Value.TrebuchetAimer_Normal;
        invoker.Modifiers.Inc(PlayerModifier.TrebuchetBoost, config.Size);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.TrebuchetAimer()
        };
    }
}
