using Backend.Users;
using Common;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Shared;

namespace Backend.Gateway;

public class UserProjectionEntryPoint : BackgroundService
{
    public UserProjectionEntryPoint(
        IConnectedUsers users,
        IClusterClient orleans,
        IServiceEnvironment environment,
        IMessagingClient messaging)
    {
        _users = users;
        _orleans = orleans;
        _environment = environment;
        _messaging = messaging;
    }

    private readonly IConnectedUsers _users;
    private readonly IClusterClient _orleans;
    private readonly IServiceEnvironment _environment;
    private readonly IMessagingClient _messaging;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lifetime = stoppingToken.ToLifetime();
        _users.Connected.Advise(lifetime, user => Task.Run(() => OnConnected(user)));
        _messaging.Listen<ProjectionPayloadValue>(lifetime, OnProjectionReceived);
        return Task.CompletedTask;
    }

    private async Task OnConnected(IUserSession user)
    {
        var projection = _orleans.GetGrain<IUserProjection>(user.UserId);

        await projection.OnConnected(_environment.ServiceId);
        await projection.ForceNotify();

        user.Lifetime.Listen(() => projection.OnDisconnected().NoAwait());
    }

    private void OnProjectionReceived(ProjectionPayloadValue payload)
    {
        if (_users.Entries.TryGetValue(payload.UserId, out var user) == false)
            return;
        
        var context = payload.Value.ToContext();

        user.Connection.Writer.WriteOneWay(new SharedBackendProjection()
        {
            Context = context
        });
    }
}