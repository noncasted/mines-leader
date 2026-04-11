using Shared;

namespace Game.GamePlay;

public class TrebuchetAimer : ICard
{
    public TrebuchetAimer(IPlayer owner, CardConfigOptions.TrebuchetAimer config)
    {
        _owner = owner;
        _config = config;
    }

    private readonly IPlayer _owner;
    private readonly CardConfigOptions.TrebuchetAimer _config;

    public CardUseResult Use()
    {
        var newValue = _owner.Modifiers.Values[PlayerModifier.TrebuchetBoost] + _config.Size;
        _owner.Modifiers.Set(PlayerModifier.TrebuchetBoost, newValue);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.TrebuchetAimer()
        };
    }
}