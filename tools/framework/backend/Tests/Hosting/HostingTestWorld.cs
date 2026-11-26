using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Lobbies;
using Cluster.Monitoring;
using Common.Network;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Session;
using Game.Hosting;
using Game.Hosting.Collection;
using Game.Lobbies;
using Game.Session;
using Internal;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;

namespace Framework.Tests.Hosting;

// One game server of the hosting module: the real SessionsCollection, LobbySessions and LobbyCodeLeases over an
// in-memory code registry, with real session containers (TestSession: AddSessionServices + the lobby services) on
// manual queues and a shared manual clock. The world plays the game: it builds the sessions (ILobbyFactory,
// ILobbyMatches, IKnownUsersMatches), searches them (ISessionSearch) and decides when a running match lets a
// late player in (TestLobbyEntryPolicy).
public class HostingTestWorld : ILobbyFactory, ILobbyMatches, IKnownUsersMatches, ISessionSearch
{
    // returnsAfterMatch: LobbyOptions.ReturnsAfterMatch - the code of a lobby comes back from its match to a new lobby.
    public HostingTestWorld(int maxUsers = 4, bool returnsAfterMatch = false)
    {
        Options = new LobbyOptions
        {
            MaxUsers = maxUsers,
            ReturnsAfterMatch = returnsAfterMatch
        };

        Self = new GameServerOverview
        {
            Id = ServerId,
            Tag = TestRoles.Game,
            UpdateTime = Time.GetUtcNow().UtcDateTime,
            Url = ServerUrl
        };

        _servers.Add(ServerId, Self);

        Discovery = Substitute.For<IServiceDiscovery>();
        Discovery.Self.Returns(Self);
        Discovery.Entries.Returns(_servers);

        Collection = new SessionsCollection(
            Substitute.For<IServiceSlicedState<LiveSessionsData, List<LiveSessionEntry>>>(),
            Time);

        ILobbyCodeAfterMatch afterMatch = returnsAfterMatch == true
            ? new LobbyReturns(this, Options, NullLogger<LobbyReturns>.Instance)
            : new LobbyCodeRelease();

        Leases = new LobbyCodeLeases(Codes, afterMatch, NullLogger<LobbyCodeLeases>.Instance);

        Lobbies = new LobbySessions(
            this,
            Collection,
            Codes,
            Leases,
            EntryPolicy,
            Options,
            Discovery,
            NullLogger<LobbySessions>.Instance);
    }

    public const string ServerUrl = "http://localhost:7102";

    // The reservation time of every session: the default of the framework options.
    public static readonly TimeSpan JoinTimeout = new SessionCreateOptions
    {
        Type = SessionType.Match,
        ExpectedUsers = 0,
        MaxUsers = 1,
        IsPrivate = false
    }.JoinTimeout;

    public readonly ManualTimeProvider Time = new();
    public readonly Guid ServerId = Guid.NewGuid();
    public readonly GameServerOverview Self;

    public readonly IServiceDiscovery Discovery;
    public readonly LobbyOptions Options;
    public readonly SessionsCollection Collection;
    public readonly FakeLobbyCodes Codes = new();
    public readonly LobbyCodeLeases Leases;
    public readonly TestLobbyEntryPolicy EntryPolicy = new();
    public readonly LobbySessions Lobbies;

    // What the service discovery knows (the meta reads it): this server, until a test removes it.
    private readonly Dictionary<Guid, IServiceOverview> _servers = new();
    private readonly Dictionary<Guid, TestSession> _sessions = new();

    // The queue each public match of the search was created for; a match created without the search is in queue 0.
    private readonly Dictionary<Guid, int> _matchQueues = new();

    // Every creation the lobby factory got, in order (the failed ones too).
    private readonly List<LobbyCreation> _lobbyCreations = new();

    // Not null: the lobby factory of the game fails with it.
    private Exception? _lobbyCreationFailure;
    private Exception? _matchCreationFailure;

    // The leader the last match was created for (ILobbyMatches.Create).
    public Guid MatchLeaderId { get; private set; }

    public string WebSocketUrl => ServerUrl.ServerUrlToWebSocket();

    public IReadOnlyList<LobbyCreation> LobbyCreations
    {
        get
        {
            lock (_lobbyCreations)
                return _lobbyCreations.ToList();
        }
    }

    // A copy: the sessions are added from the queues of the sessions too.
    public IReadOnlyDictionary<Guid, TestSession> Sessions
    {
        get
        {
            lock (_sessions)
                return new Dictionary<Guid, TestSession>(_sessions);
        }
    }

    // The game server leaves the service discovery: the meta knows no server.
    public void RemoveServers()
    {
        _servers.Clear();
    }

    // The place check of the gateway path (after the ticket): null is the refusal.
    public IUser? Enter(Guid sessionId, Guid userId)
    {
        if (Collection.TryGet(sessionId, out _) == false)
            return null;

        return Sessions[sessionId].Enter(userId);
    }

    // The user leaves (SharedSessionPlayer.Leave, then the socket closes): its seat ends in the session queue.
    public void Disconnect(TestSession session, IUser user)
    {
        session.Leave(user);
    }

    public void DrainAll()
    {
        List<TestSession> sessions;

        lock (_sessions)
            sessions = _sessions.Values.ToList();

        foreach (var session in sessions)
            session.Queue.Drain();
    }

    // A public match of the search (the game without the menu).
    public TestSession CreatePublicMatch()
    {
        return Add(MatchOptions(isPrivate: false, maxUsers: Options.MaxUsers), _ => { });
    }

    public void FailLobbyCreation(Exception failure)
    {
        _lobbyCreationFailure = failure;
    }

    public void FailMatchCreation(Exception? failure)
    {
        _matchCreationFailure = failure;
    }

    // ILobbyFactory: a lobby session with the lobby service of the framework.
    ISession ILobbyFactory.Create(LobbyCreation creation)
    {
        lock (_lobbyCreations)
            _lobbyCreations.Add(creation);

        if (_lobbyCreationFailure != null)
            throw _lobbyCreationFailure;

        var options = new SessionCreateOptions
        {
            Id = creation.Id,
            Type = SessionType.Lobby,
            ExpectedUsers = 0,
            MaxUsers = creation.MaxUsers,
            IsPrivate = true,
            Time = Time
        };

        var session = Add(options, services => {
            services.AddSingleton<ILobbyMatches>(this);
            services.AddSingleton<ILobbyListings>(Leases);

            services.AddLobbyService(new LobbySessionInfo
            {
                SessionId = creation.Id,
                Code = creation.Code,
                LobbySessionId = creation.LobbySessionId,
                RunningMatchId = creation.RunningMatchId,
                Return = creation.Return,
                MaxUsers = creation.MaxUsers
            });
        });

        return session.Session;
    }

    // ILobbyMatches: what the game does at zero of the countdown - a private match with a place for every user of the
    // lobby, listed by the code of the lobby; a closed room of one player is a match of one place.
    public LobbyMatch Create(Guid lobbySessionId, Guid leaderId, IReadOnlyList<Guid> userIds, bool isOpen)
    {
        if (_matchCreationFailure != null)
            throw _matchCreationFailure;

        MatchLeaderId = leaderId;

        if (userIds.Count > Options.MaxUsers)
            throw new InvalidOperationException($"A lobby starts a match for {userIds.Count} users, the limit is {Options.MaxUsers}");

        var maxUsers = isOpen == false && userIds.Count == 1 ? 1 : Options.MaxUsers;

        var session = Add(MatchOptions(isPrivate: true, maxUsers), services => {
            services.AddSingleton<ILobbyListings>(Leases);

            services.AddMatchListingService(new MatchListingInfo
            {
                LobbySessionId = lobbySessionId,
                IsOpen = isOpen,
                MaxUsers = Options.MaxUsers
            });
        });

        foreach (var userId in userIds)
        {
            if (session.Session.Reserve(userId) != SessionReservation.Reserved)
                throw new InvalidOperationException($"Created match session {session.Session.Id} rejected the reservation");
        }

        Leases.PassToMatch(lobbySessionId, session.Session, leaderId, userIds);

        return new LobbyMatch
        {
            ServerUrl = WebSocketUrl,
            SessionId = session.Session.Id
        };
    }

    public LobbyMatch? Admit(Guid matchSessionId, Guid userId)
    {
        if (Collection.TryGet(matchSessionId, out var session) == false || session.Type != SessionType.Match)
            return null;

        if (session.Reserve(userId) != SessionReservation.Reserved)
            return null;

        return new LobbyMatch
        {
            ServerUrl = WebSocketUrl,
            SessionId = matchSessionId
        };
    }

    // IKnownUsersMatches: a private match for exactly these users, their places reserved at the creation.
    ISession IKnownUsersMatches.Create(IReadOnlyList<Guid> userIds)
    {
        if (userIds.Count == 0 || userIds.Distinct().Count() != userIds.Count)
            throw new ArgumentException($"A match for known users needs distinct users, got {userIds.Count}", nameof(userIds));

        var session = Add(new SessionCreateOptions
        {
            Type = SessionType.Match,
            ExpectedUsers = userIds.Count,
            MaxUsers = userIds.Count,
            IsPrivate = true,
            Time = Time
        }, _ => { });

        foreach (var userId in userIds)
        {
            if (session.Session.Reserve(userId) != SessionReservation.Reserved)
                throw new InvalidOperationException($"Created match session {session.Session.Id} rejected the reservation of {userId}");
        }

        return session.Session;
    }

    // ISessionSearch: a public match of the same queue with a free place, otherwise a new one. Nothing of the game: no
    // late entry.
    public Guid GetOrCreate(Guid userId, int queue, out bool isLobby)
    {
        isLobby = false;

        foreach (var (_, session) in Collection.Entries)
        {
            if (session.Type != SessionType.Match || session.IsPrivate == true)
                continue;

            if (_matchQueues.GetValueOrDefault(session.Id) != queue)
                continue;

            if (session.Reserve(userId) == SessionReservation.Reserved)
                return session.Id;
        }

        var created = CreatePublicMatch();
        _matchQueues.Add(created.Session.Id, queue);

        if (created.Session.Reserve(userId) != SessionReservation.Reserved)
            throw new InvalidOperationException($"Created session {created.Session.Id} rejected the reservation");

        return created.Session.Id;
    }

    private SessionCreateOptions MatchOptions(bool isPrivate, int maxUsers)
    {
        return new SessionCreateOptions
        {
            Type = SessionType.Match,
            ExpectedUsers = 0,
            MaxUsers = maxUsers,
            IsPrivate = isPrivate,
            Time = Time
        };
    }

    // Same order as the factory of a game: the session is in the collection before its services start and it runs.
    private TestSession Add(SessionCreateOptions options, Action<IServiceCollection> configure)
    {
        var session = new TestSession(options, configure);

        lock (_sessions)
            _sessions.Add(session.Session.Id, session);

        Collection.Add(session.Session);
        var created = session.Queue.SetupServices(session.ServiceFactory);
        session.Queue.Drain();
        created.GetAwaiter().GetResult();
        session.Run();

        return session;
    }
}
