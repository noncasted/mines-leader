using Shared;

namespace Game.GamePlay;

public class Lockdown : ICard
{
    public Lockdown(IPlayer opponent, CardConfigOptions.Lockdown config, IRoundActionService roundActionService)
    {
        _opponent = opponent;
        _config = config;
        _roundActionService = roundActionService;
    }

    private readonly IPlayer _opponent;
    private readonly CardConfigOptions.Lockdown _config;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use()
    {
        var originalMax = _opponent.Moves.Max;
        var newMax = Math.Max(0, originalMax - _config.MovesReduction);
        _opponent.Moves.SetMax(newMax);

        var disposeAction = new LockdownDisposeAction(_opponent, originalMax);
        _roundActionService.Schedule(disposeAction, _config.Duration);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Lockdown
            {
                TargetPlayer = _opponent.User.Id
            }
        };
    }
}

public class LockdownDisposeAction : IRoundAction
{
    public LockdownDisposeAction(IPlayer opponent, int originalMax)
    {
        _opponent = opponent;
        _originalMax = originalMax;
    }

    private readonly IPlayer _opponent;
    private readonly int _originalMax;

    public void Execute()
    {
        _opponent.Moves.SetMax(_originalMax);
    }
}