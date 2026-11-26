using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Lobbies;

// The lobbies of the late players of the running matches: one per match, created by the first late player, replaced
// when it is closed or closing.
internal class RunningLobbies
{
    public RunningLobbies(ILobbyFactory factory, LobbyReservations reservations, int maxUsers, ILogger logger)
    {
        _factory = factory;
        _reservations = reservations;
        _maxUsers = maxUsers;
        _logger = logger;
    }

    private readonly ILobbyFactory _factory;
    private readonly LobbyReservations _reservations;
    private readonly int _maxUsers;
    private readonly ILogger _logger;

    // Running match -> the lobby of its late players, while that lobby is alive.
    private readonly Dictionary<Guid, ISession> _lobbies = new();
    private readonly Lock _lock = new();

    // closing: the lobby of the match the caller has seen refusing as a closing one (null on the first try), replaced even
    // before its close is visible here.
    public ISession GetOrCreate(Guid lobbySessionId, Guid matchSessionId, string code, ISession? closing)
    {
        lock (_lock)
        {
            if (_lobbies.TryGetValue(matchSessionId, out var existing) == true && existing.IsClosed == false && existing != closing)
                return existing;

            var lobby = _factory.Create(new LobbyCreation
            {
                Id = Guid.NewGuid(),
                Code = code,
                MaxUsers = _maxUsers,
                LobbySessionId = lobbySessionId,
                RunningMatchId = matchSessionId
            });

            if (lobby.Type != SessionType.Lobby)
                throw new InvalidOperationException($"The lobby factory built {lobby.Type} session {lobby.Id} for the late players of match {matchSessionId}");

            _lobbies[matchSessionId] = lobby;

            lobby.Lifetime.Listen(() => {
                _reservations.Forget(lobby);

                lock (_lock)
                {
                    if (_lobbies.TryGetValue(matchSessionId, out var current) == true && current == lobby)
                        _lobbies.Remove(matchSessionId);
                }
            });

            _logger.LogInformation("[Lobby] Lobby {SessionID} of the late players of match {MatchSessionID} created",
                lobby.Id,
                matchSessionId);

            return lobby;
        }
    }
}
