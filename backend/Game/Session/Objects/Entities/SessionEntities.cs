namespace Game;

public interface ISessionEntities
{
    IReadOnlyDictionary<int, IEntity> Entries { get; }
    IReadOnlyDictionary<IUser, IReadOnlyList<IEntity>> ByUser { get; }

    void Add(IEntity entity);
}

public class SessionEntities : ISessionEntities
{
    private readonly Dictionary<int, IEntity> _entries = new();
    private readonly Dictionary<IUser, IReadOnlyList<IEntity>> _byUser = new();

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
}

public static class SessionEntitiesExtensions
{
    public static int CountByUser(this ISessionEntities collection, IUser user)
    {
        return collection.ByUser.TryGetValue(user, out var entities) ? entities.Count : 0;
    }
}