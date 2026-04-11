using Shared;

namespace Game.GamePlay;

/// <summary>
/// Reduces the mana cost of all cards this turn via the AllCardsDiscount modifier, removed at end of round.
/// </summary>
public class PowerSurge : ICard {
    public PowerSurge(IPlayer owner, CardConfigOptions.PowerSurge config, IRoundActionService roundActionService) {
        _owner = owner;
        _config = config;
        _roundActionService = roundActionService;
    }

    private readonly IPlayer _owner;
    private readonly CardConfigOptions.PowerSurge _config;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use() {
        _owner.Modifiers.Set(PlayerModifier.AllCardsDiscount,
            _owner.Modifiers.Get(PlayerModifier.AllCardsDiscount) + _config.Discount);

        var disposeAction = new PowerSurgeDisposeAction(_owner, _config.Discount);
        _roundActionService.Schedule(disposeAction, 1);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.PowerSurge() {
                TargetPlayer = _owner.User.Id
            }
        };
    }
}

public class PowerSurgeDisposeAction : IRoundAction {
    public PowerSurgeDisposeAction(IPlayer owner, int discount) {
        _owner = owner;
        _discount = discount;
    }

    private readonly IPlayer _owner;
    private readonly int _discount;

    public void Execute() {
        var current = _owner.Modifiers.Get(PlayerModifier.AllCardsDiscount);
        _owner.Modifiers.Set(PlayerModifier.AllCardsDiscount, Math.Max(0, current - _discount));
    }
}
