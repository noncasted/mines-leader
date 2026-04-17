namespace Game.GamePlay;

public interface IRoundAction
{
    void Execute(MoveSnapshot snapshot);
}

public interface IRoundActionService
{
    void Schedule(IRoundAction action, int rounds);
    void Tick(MoveSnapshot snapshot);
}

public class RoundActionService : IRoundActionService
{
    private readonly List<Entry> _scheduledActions = new();

    public void Schedule(IRoundAction action, int rounds)
    {
        if (rounds <= 0)
            return;

        _scheduledActions.Add(new Entry { Id = Guid.NewGuid(), Action = action, RoundsLeft = rounds });
    }

    public void Tick(MoveSnapshot snapshot)
    {
        var toRemove = new List<Guid>();

        foreach (var entry in _scheduledActions)
        {
            entry.RoundsLeft--;

            if (entry.RoundsLeft == 0)
            {
                entry.Action.Execute(snapshot);
                toRemove.Add(entry.Id);
            }
        }

        foreach (var id in toRemove)
            _scheduledActions.RemoveAll(e => e.Id == id);
    }

    private class Entry
    {
        public required Guid Id { get; init; }
        public required IRoundAction Action { get; init; }
        public int RoundsLeft { get; set; }
    }
}