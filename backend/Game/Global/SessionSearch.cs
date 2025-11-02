using Game.Session;
using Shared;

namespace Game.Global;

public interface ISessionSearch
{
    Guid GetOrCreateLobby();
}

public class SessionSearch : ISessionSearch
{
    public SessionSearch(ISessionFactory factory, ISessionsCollection collection)
    {
        _factory = factory;
        _collection = collection;
    }

    private readonly ISessionFactory _factory;
    private readonly ISessionsCollection _collection;

    public Guid GetOrCreateLobby()
    {
        foreach (var (id, session) in _collection.Entries)
        {
            if (session.Type != SessionType.Lobby || session.Lifetime.IsTerminated == true)
                continue;

            return id;
        }

        return _factory.CreateLobby(new LobbyCreateOptions());
    }
}