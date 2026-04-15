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

        invoker.Modifiers.Inc(PlayerModifier.SoulLink, 1);

        _roundActionService.Schedule(new SoulLinkDisposeAction(invoker, opponent), config.Duration);

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
    public SoulLinkDisposeAction(IPlayer invoker, IPlayer opponent)
    {
        _invoker = invoker;
        _opponent = opponent;
    }

    private readonly IPlayer _invoker;
    private readonly IPlayer _opponent;

    public void Execute()
    {
        _invoker.Modifiers.Dec(PlayerModifier.SoulLink, 1);
    }
}