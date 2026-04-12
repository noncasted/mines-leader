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

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Overclock payload)
    {
        var config = _configs.Value.Overclock_Normal;

        invoker.Modifiers.Inc(PlayerModifier.AdditionalMoves, config.ExtraMoves);

        _roundActionService.Schedule(
            new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMoves, config.ExtraMoves), 1);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Overclock()
            {
                TargetPlayer = invoker.User.Id
            }
        };
    }
}