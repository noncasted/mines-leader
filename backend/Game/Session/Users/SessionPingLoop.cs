using Common;

namespace Game;

public interface ISessionPingLoop
{
    Task Run(IReadOnlyLifetime lifetime);
}

public class SessionPingLoop : ISessionPingLoop
{
    public SessionPingLoop(ISessionUsers users)
    {
        _users = users;
    }

    private readonly ISessionUsers _users;

    public Task Run(IReadOnlyLifetime lifetime)
    {
        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            foreach (var user in _users)
            {
                var isAlive = await user.Connection.Ping.Execute();
                
                if (isAlive == true)
                    continue;
                
                user.Connection.OnPingFailed();
            }

            await Task.Delay(TimeSpan.FromSeconds(1f), lifetime.Token);
        }
    }
}