using FluentAssertions;
using Framework.Tests.Session;
using Cluster.Lobbies;
using Game.Lobbies;
using Game.Session;
using Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;
using Xunit;

namespace Framework.Tests.Hosting;

// The surface a game derives from: the lobby service of the late players of a running match (Admit, ClearGame,
// MarkRunOver, OnLatePlayersStarted), through a derived service in a real session on manual time and queue.
public class LobbyLatePlayersServiceTests
{
    [Fact]
    public void Started_DerivedServiceDecides_NothingIsReported()
    {
        var (_, _, service) = Create(isRunning: true);

        service.LatePlayersStarted.Should().Be(1);
        service.GameStarted.Should().BeFalse();
        service.RunOver.Should().BeFalse();
        service.SessionUsers.Should().BeEmpty();
        service.Queue.Should().NotBeNull();
        service.Log.Should().NotBeNull();
        service.SessionInfo.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void NotRunning_LatePlayersAreNotStarted_AdmitThrows()
    {
        var (_, _, service) = Create(isRunning: false);

        service.LatePlayersStarted.Should().Be(0);

        var admit = () => service.AdmitUser(Guid.NewGuid());

        admit.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Admit_FirstPlaceWritesTheMatchToGame_TheNextKeepsIt()
    {
        var (world, match, service) = Create(isRunning: true);

        service.AdmitUser(Guid.NewGuid());

        var game = service.Read<LobbyGameState>(SharedLobbyService.GamePropertyId);
        game.SessionId.Should().Be(match.Session.Id);
        game.ServerUrl.Should().Be(world.WebSocketUrl);
        game.PlayersCount.Should().Be(1);
        service.GameStarted.Should().BeTrue();

        var version = service.Properties[SharedLobbyService.GamePropertyId].Version;

        service.AdmitUser(Guid.NewGuid());

        service.Properties[SharedLobbyService.GamePropertyId].Version.Should().Be(version);
    }

    [Fact]
    public void Admit_MatchHasNoPlace_GameStaysEmpty()
    {
        var (_, match, service) = Create(isRunning: true);

        match.Lifetime.Terminate();
        match.Queue.Drain();

        service.AdmitUser(Guid.NewGuid());

        service.GameStarted.Should().BeFalse();
    }

    [Fact]
    public void ClearGame_EntryIsOver_UsersWaitForTheNext()
    {
        var (_, _, service) = Create(isRunning: true);

        service.AdmitUser(Guid.NewGuid());
        service.ClearEntry();

        service.GameStarted.Should().BeFalse();
        service.Read<LobbyGameState>(SharedLobbyService.GamePropertyId).SessionId.Should().Be(Guid.Empty);

        service.AdmitUser(Guid.NewGuid());

        service.GameStarted.Should().BeTrue();
    }

    // Nothing to clear: the version of the property stays.
    [Fact]
    public void ClearGame_NoGame_NothingIsWritten()
    {
        var (_, _, service) = Create(isRunning: true);
        var version = service.Properties[SharedLobbyService.GamePropertyId].Version;

        service.ClearEntry();

        service.Properties[SharedLobbyService.GamePropertyId].Version.Should().Be(version);
    }

    [Fact]
    public void MarkRunOver_LobbyStateTellsIt_GameIsCleared()
    {
        var (_, _, service) = Create(isRunning: true);

        service.AdmitUser(Guid.NewGuid());
        service.EndRun();

        service.RunOver.Should().BeTrue();
        service.Read<LobbyState>(SharedLobbyService.LobbyPropertyId).IsRunOver.Should().BeTrue();
        service.GameStarted.Should().BeFalse();
    }

    private static (HostingTestWorld World, TestSession Match, TestLatePlayersService Service) Create(bool isRunning)
    {
        var world = new HostingTestWorld();
        var match = world.CreatePublicMatch();
        var id = Guid.NewGuid();

        var session = new TestSession(
            new SessionCreateOptions
            {
                Id = id,
                Type = SessionType.Lobby,
                ExpectedUsers = 0,
                MaxUsers = 4,
                IsPrivate = true,
                Time = world.Time
            },
            services => {
                services.AddSingleton<ILobbyMatches>(world);
                services.AddSingleton<ILobbyListings>(world.Leases);

                services.AddLobbyService<TestLatePlayersService>(new LobbySessionInfo
                {
                    SessionId = id,
                    Code = isRunning == true ? string.Empty : LobbyCodeGenerator.Generate(),
                    LobbySessionId = Guid.Empty,
                    RunningMatchId = isRunning == true ? match.Session.Id : Guid.Empty,
                    MaxUsers = 4
                });
            });

        var created = session.Queue.SetupServices(session.ServiceFactory);
        session.Queue.Drain();
        created.GetAwaiter().GetResult();

        return (world, match, session.Provider.GetServices<IService>().OfType<TestLatePlayersService>().Single());
    }

    private sealed class TestLatePlayersService : LobbyService
    {
        public TestLatePlayersService(
            ISessionOwner owner,
            ISessionUsers users,
            ISessionAdmission admission,
            ILobbyMatches matches,
            ILobbyListings listings,
            IExecutionQueue executionQueue,
            TimeProvider time,
            LobbySessionInfo info,
            ILogger<LobbyService> logger) : base(owner, users, admission, matches, listings, executionQueue, time, info, logger)
        {
        }

        public int LatePlayersStarted { get; private set; }
        public bool GameStarted => IsGameStarted;
        public bool RunOver => IsRunOver;
        public ISessionUsers SessionUsers => Users;
        public IExecutionQueue Queue => ExecutionQueue;
        public ILogger Log => Logger;
        public LobbySessionInfo SessionInfo => Info;

        public void AdmitUser(Guid userId)
        {
            Admit(userId);
        }

        public void ClearEntry()
        {
            ClearGame();
        }

        public void EndRun()
        {
            MarkRunOver();
        }

        protected override void OnLatePlayersStarted(IReadOnlyLifetime lifetime)
        {
            LatePlayersStarted++;
        }
    }
}
