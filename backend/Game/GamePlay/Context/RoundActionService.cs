namespace Game.GamePlay;

public interface IRoundAction
{
    /// <summary>
    /// The player whose moves the duration is counted by: for modifiers it is their carrier,
    /// for effects on cells it is the board owner. Otherwise a debuff on the opponent would expire
    /// during the caster's turn, before the target's turn.
    /// </summary>
    Guid OwnerId { get; }

    bool Tick(MoveSnapshot snapshot);
}

public interface IRoundActionService
{
    void Schedule(IRoundAction action);
    void Tick(MoveSnapshot snapshot, Guid ownerId);
}

public class RoundActionService : IRoundActionService
{
    private readonly List<IRoundAction> _scheduledActions = new();

    public void Schedule(IRoundAction action)
    {
        _scheduledActions.Add(action);
    }

    public void Tick(MoveSnapshot snapshot, Guid ownerId)
    {
        var toRemove = new List<int>();

        for (var i = 0; i < _scheduledActions.Count; i++)
        {
            var action = _scheduledActions[i];

            if (action.OwnerId != ownerId)
                continue;

            var remove = action.Tick(snapshot);
            if (remove)
                toRemove.Add(i);
        }

        for (var i = toRemove.Count - 1; i >= 0; i--)
            _scheduledActions.RemoveAt(toRemove[i]);
    }
}
