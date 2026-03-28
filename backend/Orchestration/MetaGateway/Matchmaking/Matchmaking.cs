using Cluster.Configs;
using Common;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Meta.Matches;
using MetaGateway.UserFlow;
using Shared;

namespace MetaGateway.Matchmaking;

public class SearchQueueEntry
{
    public SearchQueueEntry(Guid userId, DateTime joinedAt)
    {
        UserId = userId;
        JoinedAt = joinedAt;
    }

    public Guid UserId { get; }
    public DateTime JoinedAt { get; }
}

public interface IMatchmaking
{
    Task SearchLobby(Guid userId);
    Task SearchMatch(Guid userId, GameMatchType type);
    Task CancelMatchSearch(Guid userId);
    Task Create(Guid userId, GameMatchType type);
    Task CreateWithBot(Guid userId, GameMatchType type);
}

public class Matchmaking : IMatchmaking, ICoordinatorSetupCompleted
{
    public Matchmaking(
        IMatchFactory matchFactory,
        ILobbyFactory lobbyFactory,
        IConnectedUsers users,
        IBotConfig botConfig,
        IClusterFlags clusterFlags,
        ILogger<Matchmaking> logger)
    {
        _matchFactory = matchFactory;
        _lobbyFactory = lobbyFactory;
        _users = users;
        _botConfig = botConfig;
        _clusterFlags = clusterFlags;
        _logger = logger;

        foreach (var type in Enum.GetValues<GameMatchType>())
            _searchQueue[type] = new List<SearchQueueEntry>();
    }

    private readonly IMatchFactory _matchFactory;
    private readonly ILobbyFactory _lobbyFactory;
    private readonly IConnectedUsers _users;
    private readonly IBotConfig _botConfig;
    private readonly IClusterFlags _clusterFlags;
    private readonly ILogger<Matchmaking> _logger;
    private readonly Dictionary<GameMatchType, List<SearchQueueEntry>> _searchQueue = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _logger.LogInformation("[Matchmaking] starting matchmaking loop");
        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public Task SearchLobby(Guid userId)
    {
        _logger.LogInformation("[Matchmaking] {UserID} is searching for a lobby", userId);
        return _lobbyFactory.GetOrCreate(userId);
    }

    public async Task SearchMatch(Guid userId, GameMatchType type)
    {
        _logger.LogInformation("[Matchmaking] {UserID} is searching for a game", userId);

        await _lock.WaitAsync();

        foreach (var (_, queue) in _searchQueue)
            queue.RemoveAll(e => e.UserId == userId);

        _searchQueue[type].Add(new SearchQueueEntry(userId, DateTime.UtcNow));

        _lock.Release();
    }

    public async Task CancelMatchSearch(Guid userId)
    {
        _logger.LogInformation("[Matchmaking] {UserID} cancelled search", userId);

        await _lock.WaitAsync();

        foreach (var (_, queue) in _searchQueue)
            queue.RemoveAll(e => e.UserId == userId);

        _lock.Release();
    }

    public Task Create(Guid userId, GameMatchType type)
    {
        _logger.LogInformation("[Matchmaking] {UserID} is creating a match", userId);

        return _matchFactory.Create(new[] { userId }, type);
    }

    public Task CreateWithBot(Guid userId, GameMatchType type)
    {
        _logger.LogInformation("[Matchmaking] {UserID} is creating a match with bot", userId);

        return _matchFactory.CreateWithBot(userId, type);
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            if (_clusterFlags.MatchmakingEnabled == false)
            {
                await Task.Delay(500, lifetime.Token);
                continue;
            }

            await _lock.WaitAsync(lifetime.Token);

            var hasMatched = false;
            var botConfig = _botConfig.Value;
            var thresholdSeconds = botConfig.MatchmakingApplyThreshold;
            var now = DateTime.UtcNow;
            var matchesCreated = new List<(Guid[], GameMatchType)>();
            var botMatchesCreated = new List<(Guid, GameMatchType)>();

            foreach (var (type, queue) in _searchQueue)
            {
                var toRemove = new List<int>();
                var botMatches = new List<Guid>();

                for (var i = 0; i < queue.Count; i++)
                {
                    var entry = queue[i];
                    var waitTimeSeconds = (now - entry.JoinedAt).TotalSeconds;

                    if (waitTimeSeconds >= thresholdSeconds)
                    {
                        if (_users.IsConnected(entry.UserId) == true)
                        {
                            _logger.LogInformation(
                                "[Matchmaking] {UserID} waited {WaitTime}s, applying bot match",
                                entry.UserId,
                                (int)waitTimeSeconds
                            );
                            botMatches.Add(entry.UserId);
                            toRemove.Add(i);
                        }
                        else
                        {
                            _logger.LogInformation("[Matchmaking] {UserID} is not connected", entry.UserId);
                            toRemove.Add(i);
                        }
                    }
                }

                foreach (var idx in toRemove.OrderByDescending(x => x))
                    queue.RemoveAt(idx);

                if (queue.Count >= 2)
                {
                    var first = queue[0];
                    var second = queue[1];

                    if (_users.IsConnected(first.UserId) == false)
                    {
                        _logger.LogInformation("[Matchmaking] {UserID} is not connected", first.UserId);
                        queue.RemoveAt(0);
                        hasMatched = true;
                        continue;
                    }

                    if (_users.IsConnected(second.UserId) == false)
                    {
                        _logger.LogInformation("[Matchmaking] {UserID} is not connected", second.UserId);
                        queue.RemoveAt(1);
                        hasMatched = true;
                        continue;
                    }

                    queue.RemoveAt(0);
                    queue.RemoveAt(0);

                    _logger.LogInformation(
                        "[Matchmaking] {First} and {Second} are matched",
                        first.UserId,
                        second.UserId
                    );

                    matchesCreated.Add((new[] { first.UserId, second.UserId }, type));
                    hasMatched = true;
                }
                else if (botMatches.Count > 0)
                {
                    foreach (var userId in botMatches)
                        botMatchesCreated.Add((userId, type));

                    hasMatched = true;
                }
            }

            _lock.Release();

            foreach (var (participants, type) in matchesCreated)
                _matchFactory.Create(participants, type).NoAwait();

            foreach (var (userId, type) in botMatchesCreated)
                _matchFactory.CreateWithBot(userId, type).NoAwait();

            if (hasMatched == false)
                await Task.Delay(100, lifetime.Token);
        }

        _logger.LogInformation("[Matchmaking] matchmaking loop terminated");
    }
}