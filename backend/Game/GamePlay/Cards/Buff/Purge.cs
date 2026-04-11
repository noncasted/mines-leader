using Shared;

namespace Game.GamePlay;

public class Purge : ICard
{
    public Purge(IPlayer owner)
    {
        _owner = owner;
    }

    private readonly IPlayer _owner;

    public CardUseResult Use()
    {
        foreach (var cell in _owner.Board.Cells.Values)
        {
            var effects = cell.Effects.ToList();

            foreach (var effect in effects)
                cell.RemoveEffect(effect.Id);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Purge
            {
                TargetPlayer = _owner.User.Id
            }
        };
    }
}