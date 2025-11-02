using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Meta.Matches;
using MetaGateway.UserFlow;
using Shared;

namespace MetaGateway.Matchmaking;

public interface IMatchmaking
{
    Task SearchLobby(Guid userId);
    Task SearchMatch(Guid userId, GameMatchType type);
    Task CancelMatchSearch(Guid userId);
    Task Create(Guid userId, GameMatchType type);
}

public class Matchmaking : IMatchmaking, ICoordinatorSetupCompleted
{
    public Matchmaking(
        IMatchFactory matchFactory,
        ILobbyFactory lobbyFactory,
        IConnectedUsers users,
        ILogger<Matchmaking> logger)
    {
        _matchFactory = matchFactory;
        _lobbyFactory = lobbyFactory;
        _users = users;
        _logger = logger;

        foreach (var type in Enum.GetValues<GameMatchType>())
            _searchQueue[type] = new List<Guid>();
    }

    private readonly IMatchFactory _matchFactory;
    private readonly ILobbyFactory _lobbyFactory;
    private readonly IConnectedUsers _users;
    private readonly ILogger<Matchmaking> _logger;
    private readonly Dictionary<GameMatchType, List<Guid>> _searchQueue = new();
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
            queue.Remove(userId);

        _searchQueue[type].Add(userId);

        _lock.Release();
    }

    public async Task CancelMatchSearch(Guid userId)
    {
        _logger.LogInformation("[Matchmaking] {UserID} cancelled search", userId);

        await _lock.WaitAsync();

        foreach (var (_, queue) in _searchQueue)
            queue.Remove(userId);

        _lock.Release();
    }

    public Task Create(Guid userId, GameMatchType type)
    {
        _logger.LogInformation("[Matchmaking] {UserID} is creating a match", userId);

        return _matchFactory.Create(new[] { userId }, type);
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            await _lock.WaitAsync(lifetime.Token);

            var hasMatched = false;

            foreach (var (type, queue) in _searchQueue)
            {
                if (queue.Count < 2)
                    continue;

                var first = queue[0];
                var second = queue[1];

                if (_users.IsConnected(first) == false)
                {
                    _logger.LogInformation("[Matchmaking] {UserID} is not connected", first);
                    queue.RemoveAt(0);
                    _lock.Release();
                    continue;
                }

                if (_users.IsConnected(second) == false)
                {
                    _logger.LogInformation("[Matchmaking] {UserID} is not connected", second);
                    queue.RemoveAt(1);
                    _lock.Release();
                    continue;
                }

                queue.RemoveAt(0);
                queue.RemoveAt(0);

                _lock.Release();

                _logger.LogInformation("[Matchmaking] {First} and {Second} are matched", first, second);

                _matchFactory.Create(new[] { first, second }, type).NoAwait();
                hasMatched = true;
            }

            if (hasMatched == false)
            {
                _lock.Release();
                await Task.Delay(100, lifetime.Token);
            }
        }

        _logger.LogInformation("[Matchmaking] matchmaking loop terminated");
    }
}