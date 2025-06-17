namespace Game;

public interface ISessionEntities
{
    IReadOnlyDictionary<int, IEntity> Entries { get; }

    void Add(IEntity entity);
}

public class SessionEntities : ISessionEntities
{
    private readonly Dictionary<int, IEntity> _entries = new();

    public IReadOnlyDictionary<int, IEntity> Entries => _entries;

    public void Add(IEntity entity)
    {
        _entries.Add(entity.Id, entity);
        entity.Lifetime.Listen(() => _entries.Remove(entity.Id));
    }
}