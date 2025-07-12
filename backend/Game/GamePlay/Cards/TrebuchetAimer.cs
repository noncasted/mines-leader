using Shared;

namespace Game.GamePlay;

public class TrebuchetAimer : ICard
{
    public TrebuchetAimer(
        IPlayer owner,
        CardUsePayload.TrebuchetAimer payload)
    {
        _owner = owner;
        _payload = payload;
    }

    private readonly IPlayer _owner;
    private readonly CardUsePayload.TrebuchetAimer _payload;

    public EmptyResponse Use()
    {
        _owner.Modifiers.Inc(PlayerModifier.TrebuchetBoost);
        return EmptyResponse.Ok;
    }
}