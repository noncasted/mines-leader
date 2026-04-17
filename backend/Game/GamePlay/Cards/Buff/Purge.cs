using Shared;

namespace Game.GamePlay;

public class Purge : ICard<CardUsePayload.Purge>
{
    public CardUseResult Use(CardUseContext context, CardUsePayload.Purge payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var board = invoker.Board;

        var removed = new List<(Position Position, Guid EffectId)>();

        foreach (var cell in board.Cells.Values)
        {
            var effects = cell.Effects.ToList();

            foreach (var effect in effects)
            {
                cell.RemoveEffect(effect.Id);
                removed.Add((cell.Position, effect.Id));
            }
        }

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Purge
        {
            TargetPlayer = invoker.User.Id
        });

        foreach (var (position, effectId) in removed)
            snapshot.RecordEffectRemoved(board, position, effectId);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}