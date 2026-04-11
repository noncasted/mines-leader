using Shared;

namespace Game.GamePlay;

/// <summary>
/// Increases all of the opponent's card costs for their next turn via the ManaCostPenalty modifier.
/// </summary>
public class Embargo : ICard {
    public Embargo(IPlayer opponent, CardConfigOptions.Embargo config, IRoundActionService roundActionService) {
        _opponent = opponent;
        _config = config;
        _roundActionService = roundActionService;
    }

    private readonly IPlayer _opponent;
    private readonly CardConfigOptions.Embargo _config;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use() {
        _opponent.Modifiers.Set(PlayerModifier.ManaCostPenalty,
            _opponent.Modifiers.Get(PlayerModifier.ManaCostPenalty) + _config.CostIncrease);

        var disposeAction = new EmbargoDisposeAction(_opponent, _config.CostIncrease);
        _roundActionService.Schedule(disposeAction, 1);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Embargo() {
                TargetPlayer = _opponent.User.Id
            }
        };
    }
}

public class EmbargoDisposeAction : IRoundAction {
    public EmbargoDisposeAction(IPlayer opponent, int costIncrease) {
        _opponent = opponent;
        _costIncrease = costIncrease;
    }

    private readonly IPlayer _opponent;
    private readonly int _costIncrease;

    public void Execute() {
        var current = _opponent.Modifiers.Get(PlayerModifier.ManaCostPenalty);
        _opponent.Modifiers.Set(PlayerModifier.ManaCostPenalty, Math.Max(0, current - _costIncrease));
    }
}
