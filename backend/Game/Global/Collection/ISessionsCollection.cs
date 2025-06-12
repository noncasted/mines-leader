namespace Game;

public interface ISessionsCollection
{
    IReadOnlyDictionary<Guid, ISession> Entries { get; }
    
    void Add(ISession session);
    ISession Get(Guid id);
}