using Shared;

namespace Game.GamePlay;

public class TrebuchetAimer : ICard
{
    public TrebuchetAimer(IPlayer owner, CardsConfigs.TrebuchetAimer config)
    {
        _owner = owner;
        _config = config;
    }

    private readonly IPlayer _owner;
    private readonly CardsConfigs.TrebuchetAimer _config;

    public EmptyResponse Use()
    {
        var newValue = _owner.Modifiers.Values[PlayerModifier.TrebuchetBoost] + _config.Size;
        _owner.Modifiers.Set(PlayerModifier.TrebuchetBoost, newValue);
        return EmptyResponse.Ok;
    }
}