namespace Game.Session;

public interface ISessionObjects
{
    IReadOnlyDictionary<int, IObject> Entries { get; }

    void Add(IObject obj);

    // The object existed in this session and is gone (an entity destroyed by its owner or with its user). Ids are
    // not reused, so a late write or destroy of such an id is a race, not an error.
    bool IsRemoved(int id);
}

// Runs in the session execution queue only.
public class SessionObjects : ISessionObjects
{
    private readonly Dictionary<int, IObject> _entries = new();
    private readonly HashSet<int> _removed = new();

    public IReadOnlyDictionary<int, IObject> Entries => _entries;

    public void Add(IObject obj)
    {
        if (_entries.TryGetValue(obj.Id, out var existing) == true)
        {
            throw new InvalidOperationException(
                $"Object id {obj.Id} of {Describe(obj)} is taken by {Describe(existing)}");
        }

        _entries.Add(obj.Id, obj);

        obj.Lifetime.Listen(() => {
            _entries.Remove(obj.Id);
            _removed.Add(obj.Id);
        });
    }

    public bool IsRemoved(int id)
    {
        return _removed.Contains(id);
    }

    private static string Describe(IObject obj)
    {
        return obj is IService service ? $"service {service.Key}" : obj.GetType().Name;
    }
}
