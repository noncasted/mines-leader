using Cluster.Configs;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Meta.Users;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
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
        IOrleans orleans,
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

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    private readonly ICardConfigs _cardConfigs;
    private readonly ILogger<UserConnectionEntryPoint> _logger;

    public async Task OnConnected(IUserSession user)
    {
        if (_users.Entries.TryGetValue(user.UserId, out var existingUser))
        {
            var oldProjection = _orleans.GetGrain<IUserProjection>(user.UserId);
            await _orleans.InTransaction(oldProjection.OnDisconnected);
            _users.Remove(existingUser);
            existingUser.Connection.ForceDisconnect();
        }

        _users.Add(user);
        _commandsDispatcher.Run(user);

        _logger.LogInformation("[User] [EntryPoint] User {UserId} connected, initializing projection", user.UserId);

        try
        {
            var projection = _orleans.GetGrain<IUserProjection>(user.UserId);

            _logger.LogInformation("[User] [EntryPoint] Calling OnConnected for user {UserId}", user.UserId);
            await _orleans.InTransaction(projection.OnConnected);

            user.Lifetime.Listen(() => {
                // Guard: only call OnDisconnected if this session is still the active one.
                // On reconnect, the old session is removed from _users before ForceDisconnect
                // triggers this listener — prevents duplicate OnDisconnected racing with OnConnected.
                if (_users.Entries.TryGetValue(user.UserId, out var current) && current == user)
                {
                    _logger.LogInformation("[User] [EntryPoint] User {UserId} disconnecting, calling OnDisconnected",
                        user.UserId);

                    _orleans.InTransaction(projection.OnDisconnected).NoAwait();
                }
            });

            var channelId = new UserProjectionChannelId(user.UserId);

            _logger.LogInformation("[User] [EntryPoint] Setting up messaging channel for user {ChannelId}",
                channelId.ToRaw());

            await _messaging.ListenChannel<IProjectionPayload>(user.Lifetime, channelId, payload => {
                _logger.LogInformation("[User] [EntryPoint] Sending {PayloadType} to user {UserId}",
                    payload.GetType().Name,
                    user.UserId);

                var context = payload.ToContext();

                user.Connection.Writer.WriteOneWay(new SharedBackendProjection()
                {
                    Context = context
                });
            });

            _logger.LogInformation("[User] [EntryPoint] Forcing initial notify for user {UserId}", user.UserId);
            await _orleans.InTransaction(projection.ForceNotify);

            _cardConfigs.View(user.Lifetime, value => user.Connection.Writer.WriteOneWay(new SharedBackendProjection()
            {
                Context = value
            }));

            await user.Connection.Writer.WriteOneWay(new SharedConnectionCompleted());

            _logger.LogInformation("[User] [EntryPoint] User {UserId} projection setup completed successfully",
                user.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[User] [EntryPoint] Failed to setup projection for user {UserId}", user.UserId);
            throw;
        }
    }
}