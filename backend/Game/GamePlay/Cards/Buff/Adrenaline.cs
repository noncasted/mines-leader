using Shared;

namespace Game.GamePlay;

/// <summary>
/// Grants extra moves this turn.
/// </summary>
public class Adrenaline : ICard {
    public Adrenaline(IPlayer owner, CardConfigOptions.Adrenaline config) {
        _owner = owner;
        _config = config;
    }

    private readonly IPlayer _owner;
    private readonly CardConfigOptions.Adrenaline _config;

    public CardUseResult Use() {
        _owner.Moves.SetCurrent(_owner.Moves.Left + _config.ExtraMoves);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Adrenaline() {
                TargetPlayer = _owner.User.Id
            }
        };
    }
}
