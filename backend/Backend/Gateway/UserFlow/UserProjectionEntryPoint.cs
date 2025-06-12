using Backend.Users.Projections;
using Common;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using MemoryPack;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace Backend.Gateway;

public class UserProjectionEntryPoint : BackgroundService
{
    public UserProjectionEntryPoint(
        IConnectedUsers users,
        IClusterClient orleans,
        IServiceEnvironment environment,
        IMessagingClient messaging,
        IHubContext<UserHub> hub)
    {
        _users = users;
        _orleans = orleans;
        _environment = environment;
        _messaging = messaging;
        _hub = hub;
    }

    private readonly IConnectedUsers _users;
    private readonly IClusterClient _orleans;
    private readonly IServiceEnvironment _environment;
    private readonly IMessagingClient _messaging;
    private readonly IHubContext<UserHub> _hub;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lifetime = stoppingToken.ToLifetime();
        _users.Connected.Advise(lifetime, user => Task.Run(() => OnConnected(user)));
        _messaging.Listen<ProjectionPayloadValue>(lifetime, OnProjectionReceived);
        return Task.CompletedTask;
    }

    private async Task OnConnected(IUserConnection user)
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
        var raw = MemoryPackSerializer.Serialize(context);
        _hub.Clients.Client(user.ConnectionId).SendAsync("Update", raw);
    }
}