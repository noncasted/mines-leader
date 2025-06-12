namespace Game;

public interface ISessionProperties
{
    IReadOnlyDictionary<int, IObjectProperty> Entries { get; }
    
    void Add(IObjectProperty property);
}

public class SessionProperties : ISessionProperties
{
    private readonly Dictionary<int, IObjectProperty> _entries = new();

    public IReadOnlyDictionary<int, IObjectProperty> Entries => _entries;

    public void Add(IObjectProperty property)
    {
        _entries.Add(property.Id, property);
    }
}