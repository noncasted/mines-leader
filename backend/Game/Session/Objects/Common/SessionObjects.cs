namespace Game;

public interface ISessionObjects
{
    IReadOnlyDictionary<int, IObject> Entries { get; }

    void Add(IObject obj);
}

public class SessionObjects : ISessionObjects
{
    private readonly Dictionary<int, IObject> _entries = new();

    public IReadOnlyDictionary<int, IObject> Entries => _entries;

    public void Add(IObject obj)
    {
        _entries.Add(obj.Id, obj);
        obj.Lifetime.Listen(() => _entries.Remove(obj.Id));
    }
}