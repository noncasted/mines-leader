using Cluster.Coordination;
using Cluster.Discovery;
using Cluster.Lobbies;
using Common.Network;
using Game.Lobbies;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Hosting;

// The meta side of the session requests: every call is answered by its result (the response of the meta command: no
// separate projection, so a late result can't be taken for the answer to the next request). Ok carries the ticket of
// the user into the session; the user id is the one of the authenticated meta socket.
public interface ISessionRequester
{
    // A public match session with a free place, or the lobby of the late players of a running one (Result.IsLobby).
    Task<SharedSessionRequests.Result> Request(Guid userId);

    // A new private match session for the user alone, without a lobby and a code.
    Task<SharedSessionRequests.Result> CreateSolo(Guid userId);

    // A new private lobby session, the user gets a place in it and its code.
    Task<SharedSessionRequests.Result> CreateLobby(Guid userId);

    // A place in the lobby session that holds the code (in the lobby of the late players, if the lobby has started
    // its match); the refusal (NotFound / Full / Started) is a result too.
    Task<SharedSessionRequests.Result> JoinLobby(Guid userId, string code);

    // A place in the running match of the code, for a player in the lobby of its late players. Ok carries the address
    // of the match; NotFound - the match is over, Full - no free place, Started - it can't be entered now.
    Task<SharedSessionRequests.Result> JoinMatch(Guid userId, string code);

    // A ticket into a session the user got without a result: the match its lobby has started. The place is checked
    // by the game server on the entry.
    string IssueTicket(Guid userId, Guid sessionId);
}

public class SessionRequester : ISessionRequester
{
    public SessionRequester(
        IServiceDiscovery serviceDiscovery,
        IGameServerBalancer balancer,
        IMessaging messaging,
        ILobbyCodes lobbyCodes,
        ISessionTicketIssuer tickets,
        ILogger<SessionRequester> logger)
    {
        _serviceDiscovery = serviceDiscovery;
        _balancer = balancer;
        _messaging = messaging;
        _lobbyCodes = lobbyCodes;
        _tickets = tickets;
        _logger = logger;
    }

    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly IGameServerBalancer _balancer;
    private readonly IMessaging _messaging;
    private readonly ILobbyCodes _lobbyCodes;
    private readonly ISessionTicketIssuer _tickets;
    private readonly ILogger<SessionRequester> _logger;

    public async Task<SharedSessionRequests.Result> Request(Guid userId)
    {
        var targetServer = _balancer.Pick();
        var serverUrl = targetServer.Url.ServerUrlToWebSocket();

        var request = new SessionPayloads.Search.Request
        {
            UserId = userId
        };

        _logger.LogInformation("{UserId} [Session] [Meta] Requesting session from server {ServerURL}",
            userId,
            serverUrl);

        var pipeId = new MessagePipeServiceRequestId(targetServer, request.GetType());
        var response = await _messaging.SendPipe<SessionPayloads.Search.Response>(pipeId, request);

        _logger.LogInformation("{UserId} [Session] [Meta] Received session {SessionID} on server {ServerURL}, lobby: {IsLobby}",
            userId,
            response.SessionId,
            serverUrl,
            response.IsLobby);

        var result = Accepted(userId, response.SessionId, serverUrl, string.Empty);
        result.IsLobby = response.IsLobby;
        return result;
    }

    public async Task<SharedSessionRequests.Result> CreateSolo(Guid userId)
    {
        var targetServer = _balancer.Pick();
        var serverUrl = targetServer.Url.ServerUrlToWebSocket();

        // A match for a known set of users (the game server reserves their places): nobody else finds it or enters it.
        var request = new SessionPayloads.Create.Request
        {
            UserIds = [userId]
        };

        var pipeId = new MessagePipeServiceRequestId(targetServer, request.GetType());
        var response = await _messaging.SendPipe<SessionPayloads.Create.Response>(pipeId, request);

        _logger.LogInformation("{UserId} [Session] [Meta] Created solo session {SessionID} on server {ServerURL}",
            userId,
            response.SessionId,
            serverUrl);

        return Accepted(userId, response.SessionId, serverUrl, string.Empty);
    }

    public async Task<SharedSessionRequests.Result> CreateLobby(Guid userId)
    {
        var targetServer = _balancer.Pick();
        var serverUrl = targetServer.Url.ServerUrlToWebSocket();

        var request = new SessionPayloads.LobbyCreate.Request
        {
            UserId = userId
        };

        var pipeId = new MessagePipeServiceRequestId(targetServer, request.GetType());
        var response = await _messaging.SendPipe<SessionPayloads.LobbyCreate.Response>(pipeId, request);

        _logger.LogInformation("{UserId} [Lobby] [Meta] Created lobby {Code}: session {SessionID} on server {ServerURL}",
            userId,
            response.Code,
            response.SessionId,
            serverUrl);

        return Accepted(userId, response.SessionId, serverUrl, response.Code);
    }

    public async Task<SharedSessionRequests.Result> JoinLobby(Guid userId, string code)
    {
        code = LobbyCode.Normalize(code);

        var result = await Join();

        _logger.LogInformation("{UserId} [Lobby] [Meta] Join by code {Code}: {Status}, session {SessionID}",
            userId,
            code,
            result.Status,
            result.SessionId);

        return result;

        async Task<SharedSessionRequests.Result> Join()
        {
            var lobby = await FindLobby(code);

            if (lobby == null)
                return Refused(code, LobbyJoinStatus.NotFound);

            var (entry, targetServer) = lobby.Value;

            // A lobby that has left for its match (the code is held until the match is closed) leads to the lobby
            // of the late players of the match.
            var request = new SessionPayloads.LobbyJoin.Request
            {
                UserId = userId,
                SessionId = entry.SessionId,
                MatchSessionId = entry.MatchSessionId,
                Code = code
            };

            var pipeId = new MessagePipeServiceRequestId(targetServer, request.GetType());
            var response = await _messaging.SendPipe<SessionPayloads.LobbyJoin.Response>(pipeId, request);

            if (response.Status != LobbyJoinStatus.Ok)
                return Refused(code, response.Status);

            return Accepted(userId, response.SessionId, targetServer.Url.ServerUrlToWebSocket(), code);
        }
    }

    public async Task<SharedSessionRequests.Result> JoinMatch(Guid userId, string code)
    {
        code = LobbyCode.Normalize(code);

        var result = await Join();

        _logger.LogInformation("{UserId} [Lobby] [Meta] Join the running match of code {Code}: {Status}, session {SessionID}",
            userId,
            code,
            result.Status,
            result.SessionId);

        return result;

        async Task<SharedSessionRequests.Result> Join()
        {
            var lobby = await FindLobby(code);

            // The code is released with its match: the match is over.
            if (lobby == null || lobby.Value.Entry.MatchSessionId == Guid.Empty)
                return Refused(code, LobbyJoinStatus.NotFound);

            var (entry, targetServer) = lobby.Value;

            var request = new SessionPayloads.MatchJoin.Request
            {
                UserId = userId,
                LobbySessionId = entry.SessionId,
                MatchSessionId = entry.MatchSessionId
            };

            var pipeId = new MessagePipeServiceRequestId(targetServer, request.GetType());
            var response = await _messaging.SendPipe<SessionPayloads.MatchJoin.Response>(pipeId, request);

            if (response.Status != LobbyJoinStatus.Ok)
                return Refused(code, response.Status);

            return Accepted(userId, entry.MatchSessionId, targetServer.Url.ServerUrlToWebSocket(), code);
        }
    }

    public string IssueTicket(Guid userId, Guid sessionId)
    {
        return _tickets.Issue(userId, sessionId);
    }

    // The values of the wire are pinned: the client switches on them.
    public static SharedSessionRequests.EntryStatus ToWire(LobbyJoinStatus status)
    {
        return status switch
        {
            LobbyJoinStatus.Ok => SharedSessionRequests.EntryStatus.Ok,
            LobbyJoinStatus.NotFound => SharedSessionRequests.EntryStatus.NotFound,
            LobbyJoinStatus.Full => SharedSessionRequests.EntryStatus.Full,
            LobbyJoinStatus.Started => SharedSessionRequests.EntryStatus.Started,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown lobby join status")
        };
    }

    // The registry entry of the code and its game server; null when the code leads nowhere.
    private async Task<(LobbyRegistryEntry Entry, GameServerOverview Server)?> FindLobby(string code)
    {
        // The code is typed by a user: anything that is not a code never reaches the registry.
        if (LobbyCode.IsValid(code) == false)
            return null;

        var entry = await _lobbyCodes.Find(code);

        if (entry == null)
            return null;

        // The server of the lobby is gone (its codes stay in the registry until the next deploy).
        if (_serviceDiscovery.Entries.TryGetValue(entry.ServerId, out var overview) == false ||
            overview is not GameServerOverview server)
            return null;

        return (entry, server);
    }

    private SharedSessionRequests.Result Accepted(Guid userId, Guid sessionId, string serverUrl, string code)
    {
        return new SharedSessionRequests.Result
        {
            Status = SharedSessionRequests.EntryStatus.Ok,
            SessionId = sessionId,
            ServerUrl = serverUrl,
            Code = code,
            Ticket = _tickets.Issue(userId, sessionId)
        };
    }

    // Anything but Ok: SessionId, ServerUrl and Ticket are empty.
    private static SharedSessionRequests.Result Refused(string code, LobbyJoinStatus status)
    {
        if (status == LobbyJoinStatus.Ok)
            throw new ArgumentException("A refusal can't be Ok", nameof(status));

        return new SharedSessionRequests.Result
        {
            Status = ToWire(status),
            SessionId = Guid.Empty,
            ServerUrl = string.Empty,
            Code = code,
            Ticket = string.Empty
        };
    }
}
