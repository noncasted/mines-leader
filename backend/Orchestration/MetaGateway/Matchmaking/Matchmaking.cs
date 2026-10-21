using Cluster.Configs;
using Cluster.Deploy;
using Cluster.Monitoring;
using Common;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.Startup;
using Meta.Matches;
using Meta.Users;
using MetaGateway.UserFlow;
using Shared;

namespace MetaGateway.Matchmaking;

public class SearchQueueEntry
{
    public SearchQueueEntry(Guid userId, Guid requestId, DateTime joinedAt)
    {
        UserId = userId;
        RequestId = requestId;
        JoinedAt = joinedAt;
    }

    public Guid UserId { get; }

    // N10: SharedMatchmaking.SearchMatch.RequestId, echoed in the search result.
    public Guid RequestId { get; }

    public DateTime JoinedAt { get; }
}

public interface IMatchmaking
{
    Task<SharedMatchmaking.LobbyResult> SearchLobby(Guid userId);

    // The result comes later as a MatchSearchResultUpdate projection with this requestId: Matched, or Failed when
    // the match of the pair could not be created.
    Task SearchMatch(Guid userId, GameMatchType type, Guid requestId);
    Task CancelMatchSearch(Guid userId);
    Task<SharedMatchmaking.MatchResult> Create(Guid userId, GameMatchType type);
    Task<SharedMatchmaking.MatchResult> CreateWithBot(Guid userId, GameMatchType type, AgentMatchFixture? fixture = null);
    MatchmakingStatsDto GetStats();
}

public record MatchmakingStatsDto
{
    public int TotalInQueue { get; init; }
    public IReadOnlyList<MatchmakingQueueStatsDto> Queues { get; init; } = [];
}

public record MatchmakingQueueStatsDto
{
    public required string Type { get; init; }
    public required int Count { get; init; }
    public double? OldestWaitSeconds { get; init; }
}

public class Matchmaking : IMatchmaking, ICoordinatorSetupCompleted
{
    public Matchmaking(
        IMatchFactory matchFactory,
        ILobbyFactory lobbyFactory,
        IConnectedUsers users,
        IBotConfig botConfig,
        IClusterFlags clusterFlags,
        IClusterParticipantContext participantContext,
        IDeploymentState<MatchmakingLiveData> liveData,
        IMessaging messaging,
        ILogger<Matchmaking> logger)
    {
        _matchFactory = matchFactory;
        _messaging = messaging;
        _lobbyFactory = lobbyFactory;
        _users = users;
        _botConfig = botConfig;
        _clusterFlags = clusterFlags;
        _participantContext = participantContext;
        _liveData = liveData;
        _logger = logger;

        foreach (var type in Enum.GetValues<GameMatchType>())
            _searchQueue[type] = new List<SearchQueueEntry>();
    }

    private readonly IMatchFactory _matchFactory;
    private readonly ILobbyFactory _lobbyFactory;
    private readonly IConnectedUsers _users;
    private readonly IBotConfig _botConfig;
    private readonly IClusterFlags _clusterFlags;
    private readonly IClusterParticipantContext _participantContext;
    private readonly IDeploymentState<MatchmakingLiveData> _liveData;
    private readonly IMessaging _messaging;
    private readonly ILogger<Matchmaking> _logger;
    private readonly Dictionary<GameMatchType, List<SearchQueueEntry>> _searchQueue = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _logger.LogInformation("[Matchmaking] starting matchmaking loop");
        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public Task<SharedMatchmaking.LobbyResult> SearchLobby(Guid userId)
    {
        _logger.LogInformation("[Matchmaking] {UserID} is searching for a lobby", userId);
        return _lobbyFactory.GetOrCreate(userId);
    }

    public async Task SearchMatch(Guid userId, GameMatchType type, Guid requestId)
    {
        _logger.LogInformation("[Matchmaking] {UserID} is searching for a game, request {RequestID}", userId, requestId);

        await _lock.WaitAsync();

        foreach (var (_, queue) in _searchQueue)
            queue.RemoveAll(e => e.UserId == userId);

        _searchQueue[type].Add(new SearchQueueEntry(userId, requestId, DateTime.UtcNow));

        _lock.Release();

        PushLiveData();
    }

    public async Task CancelMatchSearch(Guid userId)
    {
        _logger.LogInformation("[Matchmaking] {UserID} cancelled search", userId);

        await _lock.WaitAsync();

        foreach (var (_, queue) in _searchQueue)
            queue.RemoveAll(e => e.UserId == userId);

        _lock.Release();

        PushLiveData();
    }

    public async Task<SharedMatchmaking.MatchResult> Create(Guid userId, GameMatchType type)
    {
        _logger.LogInformation("[Matchmaking] {UserID} is creating a match", userId);

        var match = await _matchFactory.Create(new[] { userId }, type);
        return match.ToResult(userId, Guid.Empty);
    }

    public async Task<SharedMatchmaking.MatchResult> CreateWithBot(
        Guid userId,
        GameMatchType type,
        AgentMatchFixture? fixture = null)
    {
        _logger.LogInformation("[Matchmaking] {UserID} is creating a match with bot", userId);

        var match = await _matchFactory.CreateWithBot(userId, type, fixture);
        return match.ToResult(userId, Guid.Empty);
    }

    public MatchmakingStatsDto GetStats()
    {
        _lock.Wait();

        try
        {
            var now = DateTime.UtcNow;
            var queues = new List<MatchmakingQueueStatsDto>();

            foreach (var (type, queue) in _searchQueue)
            {
                double? oldestWait = null;

                if (queue.Count > 0)
                    oldestWait = (now - queue[0].JoinedAt).TotalSeconds;

                queues.Add(new MatchmakingQueueStatsDto
                {
                    Type = type.ToString(),
                    Count = queue.Count,
                    OldestWaitSeconds = oldestWait
                });
            }

            return new MatchmakingStatsDto
            {
                TotalInQueue = queues.Sum(q => q.Count),
                Queues = queues
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        await _participantContext.IsInitialized.WaitTrue(lifetime);

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
            var matchesCreated = new List<(SearchQueueEntry[], GameMatchType)>();
            var botMatchesCreated = new List<(SearchQueueEntry, GameMatchType)>();

            foreach (var (type, queue) in _searchQueue)
            {
                var toRemove = new List<int>();
                var botMatches = new List<SearchQueueEntry>();

                for (var i = 0; i < queue.Count; i++)
                {
                    var entry = queue[i];
                    var waitTimeSeconds = (now - entry.JoinedAt).TotalSeconds;

                    if (waitTimeSeconds >= thresholdSeconds)
                    {
                        if (_users.IsConnected(entry.UserId) == true)
                        {
                            _logger.LogInformation("[Matchmaking] {UserID} waited {WaitTime}s, applying bot match",
                                entry.UserId,
                                (int)waitTimeSeconds);
                            botMatches.Add(entry);
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

                    _logger.LogInformation("[Matchmaking] {First} and {Second} are matched",
                        first.UserId,
                        second.UserId);

                    matchesCreated.Add((new[] { first, second }, type));
                    hasMatched = true;
                }
                else if (botMatches.Count > 0)
                {
                    foreach (var entry in botMatches)
                        botMatchesCreated.Add((entry, type));

                    hasMatched = true;
                }
            }

            _lock.Release();

            foreach (var (entries, type) in matchesCreated)
            {
                var participants = entries.Select(e => e.UserId).ToArray();
                CreateAndNotify(() => _matchFactory.Create(participants, type), entries, type).NoAwait();
            }

            foreach (var (entry, type) in botMatchesCreated)
                CreateAndNotify(() => _matchFactory.CreateWithBot(entry.UserId, type), [entry], type).NoAwait();

            if (hasMatched)
                PushLiveData();
            else
                await Task.Delay(100, lifetime.Token);
        }

        _logger.LogInformation("[Matchmaking] matchmaking loop terminated");
    }

    // The participants are already out of the queue: each gets the result of its own search, Matched or Failed.
    // A failed creation is not retried by returning the players to the queue: a lasting failure (no game server,
    // a broken grain) would spin the loop every 100 ms while the players wait without knowing. The client shows the
    // failure and the player searches again.
    private async Task CreateAndNotify(
        Func<Task<CreatedMatch>> create,
        IReadOnlyList<SearchQueueEntry> entries,
        GameMatchType type)
    {
        CreatedMatch? match = null;

        try
        {
            match = await create();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Matchmaking] Failed to create a {Type} match for {Participants}, the search fails",
                type, entries.Select(entry => entry.UserId));
        }

        foreach (var entry in entries)
        {
            var update = match == null
                ? MatchSearchResultUpdate.Failed(type, entry.RequestId)
                : MatchSearchResultUpdate.Matched(match, entry.UserId, entry.RequestId);

            // One participant's delivery failure (its meta socket is gone) does not take the result from the other.
            try
            {
                await _messaging.SendOneTimeProjection(entry.UserId, update);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Matchmaking] Failed to send the {Status} search result to {UserID}",
                    update.Status, entry.UserId);
            }
        }
    }

    private void PushLiveData()
    {
        var stats = GetStats();

        _liveData.SetValue(new MatchmakingLiveData
        {
            TotalInQueue = stats.TotalInQueue,
            Queues = stats.Queues.Select(q => new MatchmakingQueueLiveData
            {
                Type = q.Type,
                Count = q.Count,
                OldestWaitSeconds = q.OldestWaitSeconds
            }).ToList()
        }).NoAwait();
    }
}