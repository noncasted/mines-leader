using Cluster.Configs;
using Common.Extensions;
using Common.Network;
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

// Projections of a user: a snapshot of all projection sources plus the updates published to the user channel.
//
// Subscribe vs load: the channel is subscribed before the snapshot is read. Updates that arrive before the snapshot
// is written are buffered and sent right after it, so an update committed after the subscription is either in the
// snapshot or delivered after it - none is lost (one-time projections such as lobby results included).
// On a channel gap the same resync runs again: buffer, re-read the snapshot, send it, flush the buffer.
public class UserConnectionEntryPoint : IUserConnectionEntryPoint
{
    public UserConnectionEntryPoint(
        IConnectedUsers users,
        IUserCommandsDispatcher commandsDispatcher,
        IMessaging messaging,
        IOrleans orleans,
        IUserProjectionsLoader projections,
        ICardConfigs cardConfigs,
        IInGameAchievementConfig achievementConfig,
        IMatchMakingConfig matchMakingConfig,
        ICardPreviewGenerator cardPreviewGenerator,
        ILogger<UserConnectionEntryPoint> logger)
    {
        _users = users;
        _commandsDispatcher = commandsDispatcher;
        _messaging = messaging;
        _orleans = orleans;
        _projections = projections;
        _cardConfigs = cardConfigs;
        _achievementConfig = achievementConfig;
        _matchMakingConfig = matchMakingConfig;
        _cardPreviewGenerator = cardPreviewGenerator;
        _logger = logger;
    }

    private readonly IConnectedUsers _users;
    private readonly IUserCommandsDispatcher _commandsDispatcher;
    private readonly IMessaging _messaging;
    private readonly IOrleans _orleans;
    private readonly IUserProjectionsLoader _projections;
    private readonly ICardConfigs _cardConfigs;
    private readonly IInGameAchievementConfig _achievementConfig;
    private readonly IMatchMakingConfig _matchMakingConfig;
    private readonly ICardPreviewGenerator _cardPreviewGenerator;
    private readonly ILogger<UserConnectionEntryPoint> _logger;

    // `projections` (read by the handshake in UserFactory.Resolve) are not sent: they were read before the
    // subscription, an update published in between would be missing from them. The snapshot is re-read instead.
    public async Task OnConnected(IUserSession user, IReadOnlyList<IProjectionPayload> projections)
    {
        // The replaced session of the same user is closed; its lifetime end removes only its own entry.
        var replaced = _users.Add(user);
        replaced?.Connection.ForceDisconnect();

        _commandsDispatcher.Run(user);

        _logger.LogTrace("[User] [EntryPoint] User {UserId} connected, initializing projection", user.UserId);

        try
        {
            var channelId = new UserProjectionChannelId(user.UserId);
            var writer = user.Connection.Writer;
            var stream = new ProjectionStream(user, _logger);

            var cardPreviews = await _cardPreviewGenerator.GetBundlesAsync();

            stream.BeginResync();

            await _messaging.ListenChannel<IProjectionPayload>(user.Lifetime,
                channelId,
                stream.OnPublished,
                () => OnGapDetected(user, stream));

            await Resync(user, stream, new InitialCardPreviews { Bundles = cardPreviews });

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

    private async Task Resync(IUserSession user, ProjectionStream stream, INetworkContext? extra = null)
    {
        var snapshot = await _orleans.Transactions.Run(() => _projections.Load(user.UserId));
        stream.EndResync(snapshot, extra);
    }

    private void OnGapDetected(IUserSession user, ProjectionStream stream)
    {
        _logger.LogWarning("[User] [EntryPoint] Projection channel gap for user {UserId}, reloading projections",
            user.UserId);

        // Synchronously: the messages replayed after the gap must already be buffered.
        stream.BeginResync();
        ResyncAfterGap().NoAwait();

        return;

        async Task ResyncAfterGap()
        {
            try
            {
                await Resync(user, stream);
            }
            catch (Exception e)
            {
                // The client state is stale and cannot be repaired here: drop the connection, the client
                // reconnects and gets a fresh snapshot.
                _logger.LogError(e, "[User] [EntryPoint] Failed to reload projections for user {UserId}, disconnecting",
                    user.UserId);

                user.Connection.ForceDisconnect();
            }
        }
    }

    // Per connection. Writes only enqueue into the connection writer, so they run under the lock and keep the order.
    private sealed class ProjectionStream
    {
        public ProjectionStream(IUserSession user, ILogger logger)
        {
            _user = user;
            _writer = user.Connection.Writer;
            _logger = logger;
        }

        private readonly IUserSession _user;
        private readonly IConnectionWriter _writer;
        private readonly ILogger _logger;
        private readonly Lock _lock = new();

        // Updates received while a resync is in flight; null when the stream is live.
        private List<IProjectionPayload>? _buffer;
        private int _resyncs;

        public void BeginResync()
        {
            lock (_lock)
            {
                _resyncs++;
                _buffer ??= [];
            }
        }

        // An overlapping resync keeps buffering until the last one has written its snapshot.
        // `extra` is written right after the snapshot (the initial card previews).
        public void EndResync(IReadOnlyList<IProjectionPayload> snapshot, INetworkContext? extra)
        {
            lock (_lock)
            {
                if (_resyncs == 0)
                    throw new InvalidOperationException("Projection resync is not started");

                foreach (var payload in snapshot)
                    Write(payload);

                if (extra != null)
                    Write(extra);

                _resyncs--;

                if (_resyncs != 0)
                    return;

                var buffered = _buffer!;
                _buffer = null;

                foreach (var payload in buffered)
                    Write(payload);
            }
        }

        public void OnPublished(IProjectionPayload payload)
        {
            lock (_lock)
            {
                if (_buffer != null)
                {
                    _buffer.Add(payload);
                    return;
                }

                Write(payload);
            }
        }

        private void Write(IProjectionPayload payload)
        {
            _logger.LogTrace("[User] [EntryPoint] Sending {PayloadType} to user {UserId}",
                payload.GetType().Name,
                _user.UserId);

            Write(payload.ToContext());
        }

        private void Write(INetworkContext context)
        {
            // Enqueue only, completes synchronously: a closed or overflowed connection throws here.
            _writer.WriteOneWay(context.ToProjection()).GetAwaiter().GetResult();
        }
    }
}
