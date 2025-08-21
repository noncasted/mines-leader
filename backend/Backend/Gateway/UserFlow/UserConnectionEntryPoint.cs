using Backend.Users;
using Common;
using Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Shared;

namespace Backend.Gateway;

public interface IUserConnectionEntryPoint
{
    Task OnConnected(IUserSession session);
}

public class UserConnectionEntryPoint : IUserConnectionEntryPoint
{
    public UserConnectionEntryPoint(
        IConnectedUsers users,
        IUserCommandsDispatcher commandsDispatcher,
        IClusterClient orleans,
        IMessaging messaging,
        ILogger<UserConnectionEntryPoint> logger)
    {
        _users = users;
        _orleans = orleans;
        _messaging = messaging;
        _logger = logger;
        _commandsDispatcher = commandsDispatcher;
    }

    private readonly IConnectedUsers _users;
    private readonly IUserCommandsDispatcher _commandsDispatcher;

    private readonly IClusterClient _orleans;
    private readonly IMessaging _messaging;
    private readonly ILogger<UserConnectionEntryPoint> _logger;

    public async Task OnConnected(IUserSession user)
    {
        _users.Add(user);
        _commandsDispatcher.Run(user);

        _logger.LogInformation("[User] [EntryPoint] User {UserId} connected, initializing projection", user.UserId);

        try
        {
            var projection = _orleans.GetGrain<IUserProjection>(user.UserId);

            _logger.LogInformation("[User] [EntryPoint] Calling OnConnected for user {UserId}", user.UserId);
            await projection.OnConnected();

            user.Lifetime.Listen(() =>
                {
                    _logger.LogInformation("[User] [EntryPoint] User {UserId} disconnecting, calling OnDisconnected",
                        user.UserId
                    );
                    projection.OnDisconnected().NoAwait();
                }
            );

            var pipeId = new UserProjectionPipeId(user.UserId);

            _logger.LogInformation("[User] [EntryPoint] Setting up messaging pipe for user {PipeId}", pipeId.ToRaw());

            await _messaging.ListenPipe<IProjectionPayload>(user.Lifetime, pipeId, payload =>
                {
                    _logger.LogInformation("[User] [EntryPoint] Sending {PayloadType} to user {UserId}",
                        payload.GetType().Name,
                        user.UserId
                    );

                    var context = payload.ToContext();

                    user.Connection.Writer.WriteOneWay(new SharedBackendProjection()
                        {
                            Context = context
                        }
                    );
                }
            );

            _logger.LogInformation("[User] [EntryPoint] Forcing initial notify for user {UserId}", user.UserId);
            await projection.ForceNotify();

            await user.Connection.Writer.WriteOneWay(new SharedConnectionCompleted());

            _logger.LogInformation("[User] [EntryPoint] User {UserId} projection setup completed successfully",
                user.UserId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[User] [EntryPoint] Failed to setup projection for user {UserId}", user.UserId);
            throw;
        }
    }
}