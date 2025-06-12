namespace Game;

public interface ISessionEntities
{
    IReadOnlyDictionary<int, IEntity> Entries { get; }
    IReadOnlyDictionary<string, IEntity> ServiceEntities { get; }

    void Add(IEntity entity);
    void AddService(string key, IEntity entity);
}