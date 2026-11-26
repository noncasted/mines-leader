using Game.Session;
using Internal;

namespace Game.Lobbies;

// The leader of a lobby (LobbyState.OwnerIndex, the writer of the start and of the visibility, the leader of its match):
// the session owner of a new lobby (SessionLobbyLeader), the leader of the match for a returned one (ReturnedLobbyLeader).
// The rule is of the lobbies of the backend: the shared SessionOwner is read by the clients too.
internal interface ILobbyLeader : ISessionOwner
{
    // When the lobby service starts, before anybody reads the leader.
    void Start(IReadOnlyLifetime lifetime);
}

// The session owner: the first user in, then the next by index.
internal class SessionLobbyLeader : ILobbyLeader
{
    public SessionLobbyLeader(ISessionOwner owner)
    {
        _owner = owner;
    }

    private readonly ISessionOwner _owner;

    public IViewableProperty<IUser> Owner => _owner.Owner;

    public void Start(IReadOnlyLifetime lifetime)
    {
        // The session owner follows the users from the creation of the session.
    }
}
