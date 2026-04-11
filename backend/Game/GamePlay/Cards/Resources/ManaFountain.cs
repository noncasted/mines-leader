using Shared;

namespace Game.GamePlay;

/// <summary>
/// Rolls a random amount within a configured range and grants that much temporary mana this turn.
/// </summary>
public class ManaFountain : ICard {
    public ManaFountain(IPlayer owner, CardConfigOptions.ManaFountain config, IRoundActionService roundActionService, IGameRandom gameRandom) {
        _owner = owner;
        _config = config;
        _roundActionService = roundActionService;
        _gameRandom = gameRandom;
    }

    private readonly IPlayer _owner;
    private readonly CardConfigOptions.ManaFountain _config;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use() {
        var rolled = _gameRandom.Range(_owner, _config.MinMana, _config.MaxMana);

        _owner.Modifiers.Set(PlayerModifier.AdditionalMana,
            _owner.Modifiers.Get(PlayerModifier.AdditionalMana) + rolled);
        _owner.Mana.SetCurrent(_owner.Mana.Current + rolled);

        var disposeAction = new ManaFountainDisposeAction(_owner, rolled);
        _roundActionService.Schedule(disposeAction, 1);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ManaFountain() {
                TargetPlayer = _owner.User.Id,
                RolledAmount = rolled
            }
        };
    }
}

public class ManaFountainDisposeAction : IRoundAction {
    public ManaFountainDisposeAction(IPlayer owner, int gain) {
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
