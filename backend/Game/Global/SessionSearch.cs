using Game.Session;
using Shared;

namespace Game.Global;

public interface ISessionSearch
{
    // S4: the place of the user is reserved in the lobby it gets.
    Guid GetOrCreateLobby(Guid userId);
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

    public Guid GetOrCreateLobby(Guid userId)
    {
        foreach (var (id, session) in _collection.Entries)
        {
            if (session.Type != SessionType.Lobby || session.Lifetime.IsTerminated == true)
                continue;

            session.Reserve(userId);
            return id;
        }

        var lobbyId = _factory.CreateLobby(new LobbyCreateOptions());

        if (_collection.TryGet(lobbyId, out var created) == false)
            throw new InvalidOperationException($"Created lobby {lobbyId} is not in the collection");

        created.Reserve(userId);
        return lobbyId;
    }
}