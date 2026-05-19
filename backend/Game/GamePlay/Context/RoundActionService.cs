namespace Game.GamePlay;

public interface IRoundAction
{
    bool Tick(MoveSnapshot snapshot);
}

public interface IRoundActionService
{
    void Schedule(IRoundAction action);
    void Tick(MoveSnapshot snapshot);
}

public class RoundActionService : IRoundActionService
{
    private readonly List<IRoundAction> _scheduledActions = new();

    public void Schedule(IRoundAction action)
    {
        _scheduledActions.Add(action);
    }

    public void Tick(MoveSnapshot snapshot)
    {
        var toRemove = new List<int>();

        for (var i = 0; i < _scheduledActions.Count; i++)
        {
            var remove = _scheduledActions[i].Tick(snapshot);
            if (remove)
                toRemove.Add(i);
        }

        for (var i = toRemove.Count - 1; i >= 0; i--)
            _scheduledActions.RemoveAt(toRemove[i]);
    }
}
