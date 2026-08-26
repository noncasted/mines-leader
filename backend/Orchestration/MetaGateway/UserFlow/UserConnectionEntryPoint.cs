using Cluster.Configs;
using Common.Reactive;
using Game.GamePlay.CardPreviews;
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
        ILootProgressionConfig lootProgressionConfig,
        IMatchMakingConfig matchMakingConfig,
        ICardPreviewGenerator cardPreviewGenerator,
        ILogger<UserConnectionEntryPoint> logger)
    {
        _users = users;
        _orleans = orleans;
        _messaging = messaging;
        _cardConfigs = cardConfigs;
        _lootProgressionConfig = lootProgressionConfig;
        _matchMakingConfig = matchMakingConfig;
        _cardPreviewGenerator = cardPreviewGenerator;
        _logger = logger;
        _commandsDispatcher = commandsDispatcher;
    }

    private readonly IConnectedUsers _users;
    private readonly IUserCommandsDispatcher _commandsDispatcher;

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    private readonly ICardConfigs _cardConfigs;
    private readonly ILootProgressionConfig _lootProgressionConfig;
    private readonly IMatchMakingConfig _matchMakingConfig;
    private readonly ICardPreviewGenerator _cardPreviewGenerator;
    private readonly ILogger<UserConnectionEntryPoint> _logger;

    public async Task OnConnected(IUserSession user)
    {
        if (_users.Entries.TryGetValue(user.UserId, out var existingUser))
        {
            _users.Remove(existingUser);
            existingUser.Connection.ForceDisconnect();
        }

        _users.Add(user);
        _commandsDispatcher.Run(user);

        _logger.LogTrace("[User] [EntryPoint] User {UserId} connected, initializing projection", user.UserId);

        try
        {
            var channelId = new UserProjectionChannelId(user.UserId);
            var writer = user.Connection.Writer;

            await _messaging.ListenChannel<IProjectionPayload>(user.Lifetime, channelId, payload => {
                _logger.LogTrace("[User] [EntryPoint] Sending {PayloadType} to user {UserId}",
                    payload.GetType().Name,
                    user.UserId);

                var context = payload.ToContext();
                writer.WriteOneWay(context.ToProjection());
            });

            var userPayloads = await GeneratedUserProjections.GetAllUserProjections(_orleans, user.UserId);
            var projections = userPayloads.Select(t => t.ToContext().ToProjection());

            await Task.WhenAll(projections.Select(projection => writer.WriteOneWay(projection).AsTask()));

            _cardConfigs.View(user.Lifetime, value => writer.WriteOneWay(value.ToProjection()));
            _lootProgressionConfig.View(user.Lifetime, value => writer.WriteOneWay(value.ToProjection()));
            _matchMakingConfig.View(user.Lifetime, value => writer.WriteOneWay(value.ToProjection()));

            var cardPreviews = await _cardPreviewGenerator.GetBundlesAsync();
            await writer.WriteOneWay(new InitialCardPreviews { Bundles = cardPreviews }.ToProjection());

            await writer.WriteOneWay(new SharedConnectionCompleted());

            _logger.LogTrace("[User] [EntryPoint] User {UserId} projection setup completed successfully",
                user.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[User] [EntryPoint] Failed to setup projection for user {UserId}", user.UserId);
            throw;
        }
    }
}