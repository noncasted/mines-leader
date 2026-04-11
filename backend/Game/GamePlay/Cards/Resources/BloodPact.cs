using Shared;

namespace Game.GamePlay;

/// <summary>
/// Sacrifices HP to gain temporary mana and extra moves this turn.
/// </summary>
public class BloodPact : ICard {
    public BloodPact(IPlayer owner, CardConfigOptions.BloodPact config, IRoundActionService roundActionService) {
        _owner = owner;
        _config = config;
        _roundActionService = roundActionService;
    }

    private readonly IPlayer _owner;
    private readonly CardConfigOptions.BloodPact _config;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use() {
        _owner.Health.TakeDamage(_config.HpCost);

        var manaGain = _config.ManaGain;
        _owner.Modifiers.Set(PlayerModifier.AdditionalMana,
            _owner.Modifiers.Get(PlayerModifier.AdditionalMana) + manaGain);
        _owner.Mana.SetCurrent(_owner.Mana.Current + manaGain);

        _owner.Moves.SetCurrent(_owner.Moves.Left + _config.ExtraMoves);

        var disposeAction = new BloodPactDisposeAction(_owner, manaGain);
        _roundActionService.Schedule(disposeAction, 1);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.BloodPact() {
                TargetPlayer = _owner.User.Id
            }
        };
    }
}

public class BloodPactDisposeAction : IRoundAction {
    public BloodPactDisposeAction(IPlayer owner, int manaGain) {
        _owner = owner;
        _manaGain = manaGain;
    }

    private readonly IPlayer _owner;
    private readonly int _manaGain;

    public void Execute() {
        var current = _owner.Modifiers.Get(PlayerModifier.AdditionalMana);
        _owner.Modifiers.Set(PlayerModifier.AdditionalMana, Math.Max(0, current - _manaGain));
    }
}
