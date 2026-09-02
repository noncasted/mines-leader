using Game.Session;
using Shared;

namespace Game.GamePlay;

public interface IModifiers
{
    IReadOnlyDictionary<PlayerModifier, float> Values { get; }
    IReadOnlyList<IModifierSource> Sources { get; }

    void Add(MoveSnapshot snapshot, IModifierSource source);
    void Update(MoveSnapshot snapshot, IModifierSource source);
    void Remove(MoveSnapshot snapshot, Guid sourceId);
    void Remove(MoveSnapshot snapshot, PlayerModifier type);
    void RemoveOne(MoveSnapshot snapshot, PlayerModifier type);
    void Reset(MoveSnapshot snapshot, PlayerModifier type);
}

public class Modifiers : IModifiers
{
    private readonly Dictionary<Guid, IModifierSource> _sources = new();
    private readonly Dictionary<PlayerModifier, float> _values = new();
    private IPlayer? _owner;

    public Modifiers()
    {
        foreach (var type in PlayerModifierExtensions.All)
            _values[type] = 0f;
    }

    public IReadOnlyDictionary<PlayerModifier, float> Values => _values;
    public IReadOnlyList<IModifierSource> Sources => _sources.Values.ToList();

    public void BindOwner(IPlayer owner)
    {
        _owner = owner;
    }

    public void Add(MoveSnapshot snapshot, IModifierSource source)
    {
        _sources[source.Id] = source;
        Recalculate(snapshot);
        LogApplied(snapshot, source);
    }

    public void Update(MoveSnapshot snapshot, IModifierSource source)
    {
        if (_sources.ContainsKey(source.Id) == false)
            throw new InvalidOperationException($"Modifier source {source.Id} not found.");

        _sources[source.Id] = source;
        Recalculate(snapshot);
        LogTicked(snapshot, source);
    }

    public void Remove(MoveSnapshot snapshot, Guid sourceId)
    {
        if (_sources.TryGetValue(sourceId, out var source))
        {
            var removalOverview = source.GetOverview();
            removalOverview.TurnsToEnd = 0;
            if (_owner != null)
                snapshot.RecordModifierUpdate(_owner, removalOverview);
        }

        if (_sources.Remove(sourceId) == false)
            return;

        Recalculate(snapshot);

        if (source != null)
            LogEnded(snapshot, source, ModifierEndReason.Expired);
    }

    public void Remove(MoveSnapshot snapshot, PlayerModifier type)
    {
        var toRemove = _sources.Values.Where(s => s.Type == type).ToList();

        foreach (var source in toRemove)
        {
            var removalOverview = source.GetOverview();
            removalOverview.TurnsToEnd = 0;
            if (_owner != null)
                snapshot.RecordModifierUpdate(_owner, removalOverview);
            _sources.Remove(source.Id);
        }

        if (toRemove.Count == 0)
            return;

        Recalculate(snapshot);

        foreach (var source in toRemove)
            LogEnded(snapshot, source, ModifierEndReason.Consumed);
    }

    public void RemoveOne(MoveSnapshot snapshot, PlayerModifier type)
    {
        var source = _sources.Values.FirstOrDefault(s => s.Type == type);
        if (source == null)
            return;

        var removalOverview = source.GetOverview();
        removalOverview.TurnsToEnd = 0;
        if (_owner != null)
            snapshot.RecordModifierUpdate(_owner, removalOverview);
        _sources.Remove(source.Id);
        Recalculate(snapshot);
        LogEnded(snapshot, source, ModifierEndReason.Consumed);
    }

    public void Reset(MoveSnapshot snapshot, PlayerModifier type)
    {
        Remove(snapshot, type);
    }

    private void LogApplied(MoveSnapshot snapshot, IModifierSource source)
    {
        if (_owner == null)
            return;

        snapshot.SessionLogger?.LogModifierApplied(_owner.User.Id, source.GetOverview(), _values[source.Type]);
    }

    private void LogTicked(MoveSnapshot snapshot, IModifierSource source)
    {
        if (_owner == null)
            return;

        snapshot.SessionLogger?.LogModifierTicked(_owner.User.Id, source.GetOverview(), _values[source.Type]);
    }

    private void LogEnded(MoveSnapshot snapshot, IModifierSource source, ModifierEndReason reason)
    {
        if (_owner == null)
            return;

        snapshot.SessionLogger?.LogModifierEnded(_owner.User.Id, source.GetOverview(), _values[source.Type], reason);
    }

    private void Recalculate(MoveSnapshot snapshot)
    {
        foreach (var type in PlayerModifierExtensions.All)
            _values[type] = 0f;

        foreach (var source in _sources.Values)
            _values[source.Type] += source.Value;

        if (_owner == null)
            return;

        foreach (var source in _sources.Values)
            snapshot.RecordModifierUpdate(_owner, source.GetOverview());

        snapshot.RecordManaUpdate(_owner);
        snapshot.RecordHealthUpdate(_owner);
        snapshot.RecordMovesUpdate(_owner);
    }
}

public static class PlayerModifiersExtensions
{
    extension(IModifiers modifiers)
    {
        public float Get(PlayerModifier type)
        {
            return modifiers.Values[type];
        }

        public void Inc(MoveSnapshot snapshot, PlayerModifier type, float amount, string key = "", int turnsToEnd = -1)
        {
            var source = new DurationModifierSource(type, amount, key, turnsToEnd);
            modifiers.Add(snapshot, source);
        }

        public void Dec(MoveSnapshot snapshot, PlayerModifier type, float amount, string key = "", int turnsToEnd = -1)
        {
            var source = new DurationModifierSource(type, -amount, key, turnsToEnd);
            modifiers.Add(snapshot, source);
        }

        public void Reset(MoveSnapshot snapshot, PlayerModifier type)
        {
            modifiers.Reset(snapshot, type);
        }

        public void RemoveOne(MoveSnapshot snapshot, PlayerModifier type)
        {
            modifiers.RemoveOne(snapshot, type);
        }
    }
}
