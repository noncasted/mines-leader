namespace Game;

public interface ISessionsCollection
{
    IReadOnlyDictionary<Guid, ISession> Entries { get; }
    
    void Add(ISession session);
    ISession Get(Guid id);
}

public class SessionsCollection : ISessionsCollection
{
    private readonly Dictionary<Guid, ISession> _entries = new();

    public IReadOnlyDictionary<Guid, ISession> Entries => _entries; 

    public void Add(ISession session)
    {
        _entries.Add(session.Id, session);
        session.Lifetime.Listen(() => _entries.Remove(session.Id));
    }

    public ISession Get(Guid id)
    {
        return _entries[id];
    }
}