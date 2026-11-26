using Game.Session;
using Internal;

namespace Game.Lobbies;

// The leader of a lobby the code of a match has come back to (LobbySessionInfo.Return): the leader of that match while it
// is in the lobby - its entry takes the lead. Before it comes and after it leaves: the session owner (the first user in,
// then the next by index), so the lobby does not wait for a leader that may never come back.
internal class ReturnedLobbyLeader : ILobbyLeader
{
    public ReturnedLobbyLeader(ISessionOwner owner, ISessionUsers users, Guid leaderId)
    {
        _owner = owner;
        _users = users;
        _leaderId = leaderId;
    }

    private readonly ISessionOwner _owner;
    private readonly ISessionUsers _users;
    private readonly Guid _leaderId;
    // Null while nobody is in, as the session owner.
    private readonly ViewableProperty<IUser> _leader = new(null!);

    public IViewableProperty<IUser> Owner => _leader;

    public void Start(IReadOnlyLifetime lifetime)
    {
        _owner.Owner.Advise(lifetime, (_, _) => Update(null));
        _users.View(lifetime, OnUserAdded);
        Update(null);
    }

    private void OnUserAdded(IReadOnlyLifetime userLifetime, IUser user)
    {
        if (user.IsBot == true || user.BackendId != _leaderId)
            return;

        Update(null);
        userLifetime.Listen(() => Update(user));
    }

    // leaving: a connection of the leader of the match that ends now (it may still be among the users).
    private void Update(IUser? leaving)
    {
        _leader.Set(FindLeader(leaving) ?? _owner.Owner.Value);
    }

    private IUser? FindLeader(IUser? leaving)
    {
        foreach (var user in _users)
        {
            if (user != leaving && user.IsBot == false && user.BackendId == _leaderId)
                return user;
        }

        return null;
    }
}
