using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Links the invoker and opponent so that mine damage dealt to the invoker is mirrored to the opponent for a set duration.
/// </summary>
public class SoulLink : ICard<CardUsePayload.SoulLink>
{
    public SoulLink(ICardConfigs configs, IRoundActionService roundActionService, IGameContext gameContext)
    {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.SoulLink payload)
    {
        var config = _configs.Value.SoulLink_Normal;
        var opponent = _gameContext.GetOpponent(invoker);

        // TODO: Full damage interception requires OpenCellCommand integration.
        // Schedules removal of the link after the configured duration.
        _roundActionService.Schedule(new SoulLinkDisposeAction(), config.Duration);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.SoulLink()
            {
                TargetPlayer = opponent.User.Id
            }
        };
    }
}

public class SoulLinkDisposeAction : IRoundAction
{
    public void Execute()
    {
        // TODO: Remove soul link state when damage interception is implemented.
    }
}
