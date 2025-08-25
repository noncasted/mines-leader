using Common;

namespace Game;

public interface ISessionCreated
{
    Task OnSessionCreated(IReadOnlyLifetime lifetime);
}