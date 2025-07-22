using Backend.Gateway;
using Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;

namespace Backend.Matches;

public class Matchmaking : BackgroundService, IMatchmaking
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
    }

    private readonly IMatchFactory _matchFactory;
    private readonly ILobbyFactory _lobbyFactory;
    private readonly IConnectedUsers _users;
    private readonly ILogger<Matchmaking> _logger;
    private readonly List<Guid> _searchQueue = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Loop(stoppingToken).NoAwait();
        return Task.CompletedTask;
    }

    public async Task Search(Guid userId, string type)
    {
        switch (type)
        {
            case SharedMatchmaking.GameType:
            {
                _logger.LogInformation("[Matchmaking] {UserID} is searching for a game", userId);

                await _lock.WaitAsync();
                _searchQueue.Add(userId);
                _lock.Release();

                break;
            }
            case SharedMatchmaking.LobbyType:
            {
                _logger.LogInformation("[Matchmaking] {UserID} is searching for a lobby", userId);

                await _lobbyFactory.GetOrCreate(userId);
                break;
            }
            default:
            {
                _logger.LogWarning("[Matchmaking] {UserID} tried to search for an unknown type {Type}", userId, type);
                break;
            }
        }
    }

    public async Task CancelSearch(Guid userId)
    {
        _logger.LogInformation("[Matchmaking] {UserID} cancelled search", userId);

        await _lock.WaitAsync();
        _searchQueue.Remove(userId);
        _lock.Release();
    }

    public Task Create(Guid userId)
    {
        _logger.LogInformation("[Matchmaking] {UserID} is creating a match", userId);

        return _matchFactory.Create(new[] { userId });
    }

    private async Task Loop(CancellationToken cancellation)
    {
        while (cancellation.IsCancellationRequested == false)
        {
            if (_searchQueue.Count < 2)
            {
                await Task.Delay(100, cancellation);
                continue;
            }

            await _lock.WaitAsync(cancellation);

            var first = _searchQueue[0];
            var second = _searchQueue[1];

            if (_users.IsConnected(first) == false)
            {
                _logger.LogInformation("[Matchmaking] {UserID} is not connected", first);
                _searchQueue.RemoveAt(0);
                _lock.Release();
                continue;
            }

            if (_users.IsConnected(second) == false)
            {
                _logger.LogInformation("[Matchmaking] {UserID} is not connected", second);
                _searchQueue.RemoveAt(1);
                _lock.Release();
                continue;
            }

            _searchQueue.RemoveAt(0);
            _searchQueue.RemoveAt(0);

            _lock.Release();

            _logger.LogInformation("[Matchmaking] {UserID} and {UserID} are matched", first, second);

            _matchFactory.Create(new[] { first, second }).NoAwait();
        }
    }
}