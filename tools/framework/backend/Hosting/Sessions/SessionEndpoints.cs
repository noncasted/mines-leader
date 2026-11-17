using Cluster.Coordination;
using Cluster.Discovery;
using Game.Lobbies;
using Game.Session;
using Infrastructure;
using Internal;

namespace Game.Hosting;

// The handlers of the meta requests (SessionPayloads) on a game server, listening once the cluster is set up.
public class SessionEndpoints : ICoordinatorSetupCompleted
{
    public SessionEndpoints(
        IMessaging messaging,
        IKnownUsersMatches knownUsersMatches,
        ISessionSearch sessionSearch,
        ILobbySessions lobbySessions,
        IServiceDiscovery serviceDiscovery)
    {
        _messaging = messaging;
        _knownUsersMatches = knownUsersMatches;
        _sessionSearch = sessionSearch;
        _lobbySessions = lobbySessions;
        _serviceDiscovery = serviceDiscovery;
    }

    private readonly IMessaging _messaging;
    private readonly IKnownUsersMatches _knownUsersMatches;
    private readonly ISessionSearch _sessionSearch;
    private readonly ILobbySessions _lobbySessions;
    private readonly IServiceDiscovery _serviceDiscovery;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await Listen<SessionPayloads.Create.Request, SessionPayloads.Create.Response>(lifetime, Create);
        await Listen<SessionPayloads.Search.Request, SessionPayloads.Search.Response>(lifetime, Search);
        await Listen<SessionPayloads.LobbyCreate.Request, SessionPayloads.LobbyCreate.Response>(lifetime, CreateLobby);
        await Listen<SessionPayloads.LobbyJoin.Request, SessionPayloads.LobbyJoin.Response>(lifetime, JoinLobby);
        await Listen<SessionPayloads.MatchJoin.Request, SessionPayloads.MatchJoin.Response>(lifetime, JoinMatch);
    }

    private Task Listen<TRequest, TResponse>(IReadOnlyLifetime lifetime, Func<TRequest, Task<TResponse>> handler)
    {
        return _messaging.AddPipeRequestHandler(
            lifetime,
            new MessagePipeServiceRequestId(_serviceDiscovery.Self, typeof(TRequest)),
            handler);
    }

    private Task<SessionPayloads.Create.Response> Create(SessionPayloads.Create.Request request)
    {
        var session = _knownUsersMatches.Create(request.UserIds);

        return Task.FromResult(new SessionPayloads.Create.Response
        {
            SessionId = session.Id
        });
    }

    private Task<SessionPayloads.Search.Response> Search(SessionPayloads.Search.Request request)
    {
        var id = _sessionSearch.GetOrCreate(request.UserId, out var isLobby);

        return Task.FromResult(new SessionPayloads.Search.Response
        {
            SessionId = id,
            IsLobby = isLobby
        });
    }

    private async Task<SessionPayloads.LobbyCreate.Response> CreateLobby(SessionPayloads.LobbyCreate.Request request)
    {
        var created = await _lobbySessions.Create(request.UserId);

        return new SessionPayloads.LobbyCreate.Response
        {
            SessionId = created.SessionId,
            Code = created.Code
        };
    }

    private Task<SessionPayloads.LobbyJoin.Response> JoinLobby(SessionPayloads.LobbyJoin.Request request)
    {
        var joined = _lobbySessions.Join(request.SessionId, request.MatchSessionId, request.Code, request.UserId);

        return Task.FromResult(new SessionPayloads.LobbyJoin.Response
        {
            Status = joined.Status,
            SessionId = joined.SessionId
        });
    }

    private Task<SessionPayloads.MatchJoin.Response> JoinMatch(SessionPayloads.MatchJoin.Request request)
    {
        var status = _lobbySessions.JoinMatch(request.LobbySessionId, request.MatchSessionId, request.UserId);

        return Task.FromResult(new SessionPayloads.MatchJoin.Response
        {
            Status = status
        });
    }
}
