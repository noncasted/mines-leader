using Shared;

namespace Game.GamePlay;

public class Siphon : ICard
{
    public Siphon(IPlayer owner, IPlayer opponent, CardConfigOptions.Siphon config)
    {
        _owner = owner;
        _opponent = opponent;
        _config = config;
    }

    private readonly IPlayer _owner;
    private readonly IPlayer _opponent;
    private readonly CardConfigOptions.Siphon _config;

    public CardUseResult Use()
    {
        _opponent.Mana.SetMax(_opponent.Mana.Max - _config.DrainAmount);
        _owner.Mana.SetMax(_owner.Mana.Max + _config.DrainAmount);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Siphon()
            {
                TargetPlayer = _opponent.User.Id
            }
        };
    }
}