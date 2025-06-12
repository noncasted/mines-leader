namespace Game;

public class SessionEntities : ISessionEntities
{
    private readonly Dictionary<int, IEntity> _entries = new();
    private readonly Dictionary<string, IEntity> _serviceEntries = new();

    public IReadOnlyDictionary<int, IEntity> Entries => _entries;
    public IReadOnlyDictionary<string, IEntity> ServiceEntities => _serviceEntries;

    public void Add(IEntity entity)
    {
        _entries.Add(entity.Id, entity);
        entity.Lifetime.Listen(() => _entries.Remove(entity.Id));
    }

    public void AddService(string key, IEntity entity)
    {
        _serviceEntries.Add(key, entity);
        entity.Lifetime.Listen(() => _serviceEntries.Remove(key));
    }
}