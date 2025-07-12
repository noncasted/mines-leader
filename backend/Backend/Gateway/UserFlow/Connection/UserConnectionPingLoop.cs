using Common;
using ServiceLoop;

namespace Backend.Gateway;

public class UserConnectionPingLoop : ISetupLoopStage
{
    public UserConnectionPingLoop(IConnectedUsers users)
    {
        _users = users;
    }

    private readonly IConnectedUsers _users;
    
    public Task OnSetupStage(IReadOnlyLifetime lifetime)
    {
        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            
        }
    }
}