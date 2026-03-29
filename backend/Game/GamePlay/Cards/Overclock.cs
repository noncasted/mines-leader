using Shared;

namespace Game.GamePlay;

public class Overclock : ICard
{
    public Overclock(IPlayer owner, CardConfigOptions.Overclock config)
    {
        _owner = owner;
        _config = config;
    }

    private readonly IPlayer _owner;
    private readonly CardConfigOptions.Overclock _config;

    public CardUseResult Use()
    {
        _owner.Moves.SetCurrent(_owner.Moves.Left + _config.ExtraMoves);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Overclock()
            {
                TargetPlayer = _owner.User.Id
            }
        };
    }
}
