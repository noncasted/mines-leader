namespace Game.GamePlay;

public interface IRoundAction
{
    /// <summary>
    /// Игрок, по чьим ходам отсчитывается длительность: для модификаторов это их носитель,
    /// для эффектов на клетках — владелец доски. Иначе дебаф на оппонента сгорал бы
    /// ещё на ходу кастера, не дожив до хода цели.
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
