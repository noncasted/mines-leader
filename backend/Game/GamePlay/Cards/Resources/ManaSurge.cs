using Shared;

namespace Game.GamePlay;

/// <summary>
/// Grants temporary mana this turn via the AdditionalMana modifier, removed at end of round.
/// </summary>
public class ManaSurge : ICard {
    public ManaSurge(IPlayer owner, CardConfigOptions.ManaSurge config, IRoundActionService roundActionService) {
        _owner = owner;
        _config = config;
        _roundActionService = roundActionService;
    }

    private readonly IPlayer _owner;
    private readonly CardConfigOptions.ManaSurge _config;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use() {
        var gain = _config.ManaGain;

        _owner.Modifiers.Set(PlayerModifier.AdditionalMana,
            _owner.Modifiers.Get(PlayerModifier.AdditionalMana) + gain);
        _owner.Mana.SetCurrent(_owner.Mana.Current + gain);

        var disposeAction = new ManaSurgeDisposeAction(_owner, gain);
        _roundActionService.Schedule(disposeAction, 1);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ManaSurge() {
                TargetPlayer = _owner.User.Id
            }
        };
    }
}

public class ManaSurgeDisposeAction : IRoundAction {
    public ManaSurgeDisposeAction(IPlayer owner, int gain) {
        _owner = owner;
        _gain = gain;
    }

    private readonly IPlayer _owner;
    private readonly int _gain;

    public void Execute() {
        var current = _owner.Modifiers.Get(PlayerModifier.AdditionalMana);
        _owner.Modifiers.Set(PlayerModifier.AdditionalMana, Math.Max(0, current - _gain));
    }
}
