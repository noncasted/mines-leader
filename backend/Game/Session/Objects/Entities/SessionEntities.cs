namespace Game.Session;

public interface ISessionEntities
{
    IReadOnlyDictionary<int, IEntity> Entries { get; }
    IReadOnlyDictionary<IUser, IReadOnlyList<IEntity>> ByUser { get; }

    void Add(IEntity entity);
    int CountByUser(IUser user);
}

public class SessionEntities : ISessionEntities
{
    private readonly Dictionary<int, IEntity> _entries = new();
    private readonly Dictionary<IUser, IReadOnlyList<IEntity>> _byUser = new();
    private readonly Dictionary<IUser, int> _counter = new();

    public IReadOnlyDictionary<int, IEntity> Entries => _entries;
    public IReadOnlyDictionary<IUser, IReadOnlyList<IEntity>> ByUser => _byUser;

    public void Add(IEntity entity)
    {
        _entries.Add(entity.Id, entity);
        
        if (_byUser.TryGetValue(entity.Owner, out var byUser) == false)
        {
            byUser = new List<IEntity>();
            _byUser[entity.Owner] = byUser;
        }

        var userEntities = ((List<IEntity>)byUser);
        userEntities.Add(entity);
        
        entity.Lifetime.Listen(() =>
        {
            _entries.Remove(entity.Id);
            userEntities.Remove(entity);
        });
    }

    public int CountByUser(IUser user)
    {
        _counter.TryAdd(user, 0);
        _counter[user]++;
        return _counter[user];
    }
}
