using Backend.Users;
using Common;
using Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Shared;

namespace Backend.Gateway;

public class UserProjectionEntryPoint : BackgroundService
{
    public UserProjectionEntryPoint(
        IConnectedUsers users,
        IClusterClient orleans,
        IMessaging messaging)
    {
        _users = users;
        _orleans = orleans;
        _messaging = messaging;
    }

    private readonly IConnectedUsers _users;
    private readonly IClusterClient _orleans;
    private readonly IMessaging _messaging;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lifetime = stoppingToken.ToLifetime();
        _users.Connected.Advise(lifetime, user => Task.Run(() => OnConnected(user)));
        return Task.CompletedTask;
    }

    private async Task OnConnected(IUserSession user)
    {
        var projection = _orleans.GetGrain<IUserProjection>(user.UserId);

        await projection.OnConnected();
        await projection.ForceNotify();

        user.Lifetime.Listen(() => projection.OnDisconnected().NoAwait());

        var pipeId = new UserProjectionPipeId(user.UserId);
        
        _messaging.ListenPipe<IProjectionPayload>(user.Lifetime, pipeId, payload =>
        {
            var context = payload.ToContext();

            user.Connection.Writer.WriteOneWay(new SharedBackendProjection()
                {
                    Context = context
                }
            );
        });
    }
}