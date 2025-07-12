using Common;

namespace Game;

public interface ISessionEvents
{
    Task OnCreated(IReadOnlyLifetime lifetime);
    Task OnAllConnected(IReadOnlyLifetime lifetime);
}

public class SessionEvents : ISessionEvents
{
    public SessionEvents(
        IEnumerable<ISessionCreated> created,
        IEnumerable<IUsersConnected> usersConnected)
    {
        _created = created;
        _usersConnected = usersConnected;
    }
    
    private readonly IEnumerable<ISessionCreated> _created;
    private readonly IEnumerable<IUsersConnected> _usersConnected;

    public Task OnCreated(IReadOnlyLifetime lifetime)
    {
        return Task.WhenAll(_created.Select(t => t.OnSessionCreated(lifetime)));
    }

    public Task OnAllConnected(IReadOnlyLifetime lifetime)
    {
        return Task.WhenAll(_usersConnected.Select(t => t.OnUsersConnected(lifetime)));
    }
}