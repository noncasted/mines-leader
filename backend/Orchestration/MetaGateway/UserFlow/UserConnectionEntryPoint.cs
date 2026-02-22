using Cluster.Configs;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Meta.Users;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
using Orleans;
using Shared;

namespace MetaGateway.UserFlow;

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
        ICardConfigs cardConfigs,
        ILogger<UserConnectionEntryPoint> logger)
    {
        _users = users;
        _orleans = orleans;
        _messaging = messaging;
        _cardConfigs = cardConfigs;
        _logger = logger;
        _commandsDispatcher = commandsDispatcher;
    }

    private readonly IConnectedUsers _users;
    private readonly IUserCommandsDispatcher _commandsDispatcher;

    private readonly IClusterClient _orleans;
    private readonly IMessaging _messaging;
    private readonly ICardConfigs _cardConfigs;
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

            _cardConfigs.View(user.Lifetime, value => user.Connection.Writer.WriteOneWay(new SharedBackendProjection()
                    {
                        Context = value
                    }
                )
            );

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