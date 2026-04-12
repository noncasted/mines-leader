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

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Adrenaline payload)
    {
        var config = _configs.Value.Adrenaline_Normal;

        invoker.Modifiers.Inc(PlayerModifier.AdditionalMoves, config.ExtraMoves);

        _roundActionService.Schedule(
            new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMoves, config.ExtraMoves), 1);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Adrenaline()
            {
                TargetPlayer = invoker.User.Id
            }
        };
    }
}