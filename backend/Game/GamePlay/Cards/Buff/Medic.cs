using Shared;

namespace Game.GamePlay;

public class Medic : ICard
{
    public Medic(IPlayer owner)
    {
        _owner = owner;
    }

    private readonly IPlayer _owner;

    public CardUseResult Use()
    {
        _owner.Health.Heal(1);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Medic()
            {
                TargetPlayer = _owner.User.Id
            }
        };
    }
}