using Cluster.Discovery;
using Cluster.Lobbies;
using Game.Hosting.Collection;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Lobbies;

// Lobby sessions of this game server: private sessions entered by a code (or from the list of open lobbies,
// by the same code).
// A user holds a reserved place in one lobby of the server at a time: a place taken in another lobby (a new
// "Play", another code) drops the reservation in the previous one, and a lobby left with nobody is closed
// at once and frees its code. A repeated join of the same lobby only refreshes the reservation.
// The code of a lobby that has started its match leads to the lobby of the late players of that match (one per
// match, created by the first late player, closed when it is left with nobody): when the players get into the match
// is up to the game (ILobbyEntryPolicy, the LobbyService of the game). The search leads to the same lobby of a
// running public match (JoinLate), without a code.
public interface ILobbySessions
{
    // Creates a lobby session with a place reserved for the creator and a code registered for it.
    Task<LobbyCreated> Create(Guid userId);

    // Reserves a place in the lobby session the registry pointed to: the lobby itself while matchSessionId is
    // empty, otherwise the lobby of the late players of the match (Full when the match has no free place,
    // Started when it can't be entered now, NotFound when it is over).
    LobbyJoined Join(Guid lobbySessionId, Guid matchSessionId, string code, Guid userId);

    // A place in the running match of the lobby, for a late player: only when the game lets one in
    // (ILobbyEntryPolicy.CanEnterMatchNow, Started otherwise).
    LobbyJoinStatus JoinMatch(Guid lobbySessionId, Guid matchSessionId, Guid userId);

    // The search: a place in the lobby of the late players of a running public match (no code). Full when the
    // match has no free place, NotFound when it is over.
    LobbyJoined JoinLate(Guid matchSessionId, Guid userId);
}

// The composer of the lobby sessions: a lobby is created and entered here, the late players of a running match go to
// RunningMatchEntry.
public class LobbySessions : ILobbySessions
{
    public LobbySessions(
        ILobbyFactory factory,
        ISessionsCollection collection,
        ILobbyCodes codes,
        ILobbyCodeLeases codeLeases,
        ILobbyEntryPolicy entryPolicy,
        LobbyOptions options,
        IServiceDiscovery serviceDiscovery,
        ILogger<LobbySessions> logger)
    {
        _factory = factory;
        _collection = collection;
        _codes = codes;
        _codeLeases = codeLeases;
        _maxUsers = options.MaxUsers;
        _serviceDiscovery = serviceDiscovery;
        _logger = logger;

        _reservations = new LobbyReservations(logger);

        var runningLobbies = new RunningLobbies(factory, _reservations, options.MaxUsers, logger);
        _runningMatches = new RunningMatchEntry(collection, codeLeases, entryPolicy, runningLobbies, _reservations, logger);
    }

    private readonly ILobbyFactory _factory;
    private readonly ISessionsCollection _collection;
    private readonly ILobbyCodes _codes;
    private readonly ILobbyCodeLeases _codeLeases;
    private readonly int _maxUsers;
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly ILogger<LobbySessions> _logger;
    private readonly LobbyReservations _reservations;
    private readonly RunningMatchEntry _runningMatches;

    public async Task<LobbyCreated> Create(Guid userId)
    {
        // The code is registered before the session exists: the session is built with its code. A join that
        // resolves the code in between finds no session and is answered NotFound.
        var sessionId = Guid.NewGuid();
        var code = await _codes.Register(_serviceDiscovery.Self.Id, sessionId);

        ISession session;

        try
        {
            session = _factory.Create(new LobbyCreation
            {
                Id = sessionId,
                Code = code,
                MaxUsers = _maxUsers
            });

            if (session.Id != sessionId || session.Type != SessionType.Lobby)
                throw new InvalidOperationException($"The lobby factory built {session.Type} session {session.Id} for lobby {sessionId}");
        }
        catch
        {
            // No lobby holds the code: nothing would ever release it.
            await ReleaseUnheldCode(code, sessionId);
            throw;
        }

        // The code is released when the lobby is closed (the last user left or nobody entered in time) or,
        // if the lobby has started a match, when that match is closed.
        _codeLeases.Hold(code, session);
        session.Lifetime.Listen(() => _reservations.Forget(session));

        if (session.Reserve(userId) != SessionReservation.Reserved)
            throw new InvalidOperationException($"Created lobby session {sessionId} rejected the reservation");

        _reservations.OnReserved(session, userId);

        _logger.LogInformation("{UserId} [Lobby] Lobby session {SessionID} created with code {Code}",
            userId,
            sessionId,
            code);

        return new LobbyCreated
        {
            SessionId = sessionId,
            Code = code
        };
    }

    public LobbyJoined Join(Guid lobbySessionId, Guid matchSessionId, string code, Guid userId)
    {
        if (matchSessionId != Guid.Empty)
            return _runningMatches.JoinLobby(lobbySessionId, matchSessionId, code, userId);

        if (_collection.TryGet(lobbySessionId, out var session) == false || session.Type != SessionType.Lobby)
            return LobbyJoined.Refused(LobbyJoinStatus.NotFound);

        var reservation = session.Reserve(userId);

        switch (reservation)
        {
            case SessionReservation.Reserved:
                _reservations.OnReserved(session, userId);

                return new LobbyJoined
                {
                    Status = LobbyJoinStatus.Ok,
                    SessionId = session.Id
                };

            // Sealed at zero of the countdown: the match exists, but the registry has not been told yet - the next
            // join by the code gets to the match. Also for a sealed lobby its users have already left.
            case SessionReservation.Sealed:
                return LobbyJoined.Refused(LobbyJoinStatus.Started);

            // A closed session keeps its code until its lifetime is terminated from the session queue. A lobby held
            // only by the stale connection of this user closes once it is dropped: the room is gone, not full.
            case SessionReservation.Closed:
            case SessionReservation.HeldOnlyByOwnStale:
                return LobbyJoined.Refused(LobbyJoinStatus.NotFound);

            case SessionReservation.Full:
                return LobbyJoined.Refused(LobbyJoinStatus.Full);

            default:
                throw new ArgumentOutOfRangeException(nameof(reservation), reservation, null);
        }
    }

    public LobbyJoinStatus JoinMatch(Guid lobbySessionId, Guid matchSessionId, Guid userId)
    {
        return _runningMatches.JoinMatch(lobbySessionId, matchSessionId, userId);
    }

    public LobbyJoined JoinLate(Guid matchSessionId, Guid userId)
    {
        return _runningMatches.JoinLobby(Guid.Empty, matchSessionId, string.Empty, userId);
    }

    // The failure of the release is logged, not thrown: the caller gets the failure of the creation, the cause.
    private async Task ReleaseUnheldCode(string code, Guid sessionId)
    {
        try
        {
            await _codes.Release(code, sessionId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Lobby] Code {Code} of lobby session {SessionID} that was not created is not released, it stays in the registry until the next deploy",
                code,
                sessionId);
        }
    }
}
