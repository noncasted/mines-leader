using Shared;

namespace Game.GamePlay;

/// <summary>
/// Useless card injected by SabotageDeck. Always fails when played and cannot produce any effect.
/// </summary>
public class Dud : ICard
{
    public Dud(IPlayer owner)
    {
        _owner = owner;
    }

    private readonly IPlayer _owner;

    public CardUseResult Use()
    {
        return new CardUseResult
        {
            Result = EmptyResponse.Fail("Dud card cannot be used"),
            ActionData = new CardActionSnapshot.Dud()
            {
                TargetPlayer = _owner.User.Id
            }
        };
    }
}
