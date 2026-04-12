using Shared;

namespace Game.GamePlay;

public class Purge : ICard<CardUsePayload.Purge>
{
    public CardUseResult Use(IPlayer invoker, CardUsePayload.Purge payload)
    {
        foreach (var cell in invoker.Board.Cells.Values)
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
                TargetPlayer = invoker.User.Id
            }
        };
    }
}