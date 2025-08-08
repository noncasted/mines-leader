using Common;

namespace Game;

public interface IUsersConnected
{
    Task OnUsersConnected(IReadOnlyLifetime lifetime);
}