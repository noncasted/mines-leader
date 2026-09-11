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
    Task OnConnected(IUserSession session, IReadOnlyList<IProjectionPayload> projections);
}

public class UserConnectionEntryPoint : IUserConnectionEntryPoint
{
    public UserConnectionEntryPoint(
        IConnectedUsers users,
        IUserCommandsDispatcher commandsDispatcher,
        IMessaging messaging,
        ICardConfigs cardConfigs,
        IInGameAchievementConfig achievementConfig,
        IMatchMakingConfig matchMakingConfig,
        ICardPreviewGenerator cardPreviewGenerator,
        ILogger<UserConnectionEntryPoint> logger)
    {
        _users = users;
        _messaging = messaging;
        _cardConfigs = cardConfigs;
        _achievementConfig = achievementConfig;
        _matchMakingConfig = matchMakingConfig;
        _cardPreviewGenerator = cardPreviewGenerator;
        _logger = logger;
        _commandsDispatcher = commandsDispatcher;
    }

    private readonly IConnectedUsers _users;
    private readonly IUserCommandsDispatcher _commandsDispatcher;

    private readonly IMessaging _messaging;
    private readonly ICardConfigs _cardConfigs;
    private readonly IInGameAchievementConfig _achievementConfig;
    private readonly IMatchMakingConfig _matchMakingConfig;
    private readonly ICardPreviewGenerator _cardPreviewGenerator;
    private readonly ILogger<UserConnectionEntryPoint> _logger;

    public async Task OnConnected(IUserSession user, IReadOnlyList<IProjectionPayload> projections)
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

            var cardPreviews = await _cardPreviewGenerator.GetBundlesAsync();

            var initial = projections
                          .Select(t => t.ToContext().ToProjection())
                          .Append(new InitialCardPreviews { Bundles = cardPreviews }.ToProjection());

            await Task.WhenAll(initial.Select(projection => writer.WriteOneWay(projection).AsTask()));

            await _messaging.ListenChannel<IProjectionPayload>(user.Lifetime, channelId, payload => {
                _logger.LogTrace("[User] [EntryPoint] Sending {PayloadType} to user {UserId}",
                    payload.GetType().Name,
                    user.UserId);

                var context = payload.ToContext();
                writer.WriteOneWay(context.ToProjection());
            });

            _cardConfigs.View(user.Lifetime, value => writer.WriteOneWay(value.ToProjection()));
            _achievementConfig.View(user.Lifetime, value => writer.WriteOneWay(value.ToProjection()));
            _matchMakingConfig.View(user.Lifetime, value => writer.WriteOneWay(value.ToProjection()));

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
