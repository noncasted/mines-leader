using Shared;

namespace Game.GamePlay;

/// <summary>
/// Links the owner and opponent so that mine damage dealt to the owner is mirrored to the opponent for a set duration.
/// </summary>
public class SoulLink : ICard
{
    public SoulLink(
        IPlayer owner,
        IPlayer opponent,
        CardConfigOptions.SoulLink config,
        IRoundActionService roundActionService)
    {
        _owner = owner;
        _opponent = opponent;
        _config = config;
        _roundActionService = roundActionService;
    }

    private readonly IPlayer _owner;
    private readonly IPlayer _opponent;
    private readonly CardConfigOptions.SoulLink _config;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use()
    {
        // TODO: Full damage interception requires OpenCellCommand integration.
        // Schedules removal of the link after the configured duration.
        _roundActionService.Schedule(new SoulLinkDisposeAction(), _config.Duration);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.SoulLink()
            {
                TargetPlayer = _opponent.User.Id
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
