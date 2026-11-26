using FluentAssertions;
using Framework.Tests.Session;
using Game.Hosting.Collection;
using Game.Lobbies;
using Game.Session;
using Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.Hosting;

// Lobby sessions on a game server: creation with a code, entering by the code, the code is freed with the
// session. Real sessions on manual time and queues: see HostingTestWorld.
public class LobbySessionsTests
{
    [Fact]
    public async Task Create_ReturnsValidCode_RegisteredForTheSessionOfThisServer()
    {
        var world = new HostingTestWorld();

        var created = await world.Lobbies.Create(Guid.NewGuid());

        LobbyCode.IsValid(created.Code).Should().BeTrue();

        var entry = await world.Codes.Find(created.Code);
        entry.Should().NotBeNull();
        entry!.SessionId.Should().Be(created.SessionId);
        entry.ServerId.Should().Be(world.ServerId);
    }

    [Fact]
    public async Task Create_CreatesPrivateLobbySessionOfTheConfiguredPlaces()
    {
        var world = new HostingTestWorld();

        var created = await world.Lobbies.Create(Guid.NewGuid());
        var session = world.Sessions[created.SessionId];

        session.Session.Type.Should().Be(SessionType.Lobby);
        session.Session.IsPrivate.Should().BeTrue();
        session.Options.MaxUsers.Should().Be(4);
        session.Options.ExpectedUsers.Should().Be(0);
        session.LobbyInfo().Code.Should().Be(created.Code);
        session.LobbyInfo().IsRunning.Should().BeFalse();
        world.Collection.TryGet(created.SessionId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Create_ReservesPlaceForCreator()
    {
        var world = new HostingTestWorld();
        var creator = Guid.NewGuid();

        var created = await world.Lobbies.Create(creator);
        var session = world.Sessions[created.SessionId];

        // The reservation holds the empty session until JoinTimeout and lets the creator in.
        world.Time.Advance(HostingTestWorld.JoinTimeout - TimeSpan.FromSeconds(1));
        session.Queue.Drain();

        session.Lifetime.IsTerminated.Should().BeFalse();
        world.Enter(created.SessionId, creator).Should().NotBeNull();
    }

    [Fact]
    public async Task Create_TwoLobbies_GetDifferentCodesAndSessions()
    {
        var world = new HostingTestWorld();

        var first = await world.Lobbies.Create(Guid.NewGuid());
        var second = await world.Lobbies.Create(Guid.NewGuid());

        second.Code.Should().NotBe(first.Code);
        second.SessionId.Should().NotBe(first.SessionId);
    }

    [Fact]
    public async Task Create_GeneratedCodeIsTaken_NextCodeIsUsed()
    {
        var world = new HostingTestWorld();
        var codes = new Queue<string>(["AAAAAA", "AAAAAA", "AAAAAA", "BBBBBB"]);
        world.Codes.SetGenerator(codes.Dequeue);

        var first = await world.Lobbies.Create(Guid.NewGuid());
        var second = await world.Lobbies.Create(Guid.NewGuid());

        first.Code.Should().Be("AAAAAA");
        second.Code.Should().Be("BBBBBB");
    }

    [Fact]
    public async Task Join_ByCode_ReservesPlaceInTheSameSession()
    {
        var world = new HostingTestWorld();
        var creator = Guid.NewGuid();
        var guest = Guid.NewGuid();

        var created = await world.Lobbies.Create(creator);
        var session = world.Sessions[created.SessionId];
        var owner = world.Enter(created.SessionId, creator)!;

        var entry = await world.Codes.Find(created.Code);
        world.Lobbies.JoinLobby(entry!.SessionId, guest).Should().Be(LobbyJoinStatus.Ok);

        // The reservation alone holds the session when the creator leaves.
        world.Disconnect(session, owner);
        session.Lifetime.IsTerminated.Should().BeFalse();

        world.Enter(created.SessionId, guest).Should().NotBeNull();
    }

    [Fact]
    public async Task Join_SameUserTwice_HoldsOnePlace()
    {
        var world = new HostingTestWorld();
        var guest = Guid.NewGuid();

        var created = await world.Lobbies.Create(Guid.NewGuid());

        world.Lobbies.JoinLobby(created.SessionId, guest).Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinLobby(created.SessionId, guest).Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinLobby(created.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinLobby(created.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Ok);
        world.Lobbies.JoinLobby(created.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Full);
    }

    [Fact]
    public async Task Join_UnknownCode_IsNotInRegistry()
    {
        var world = new HostingTestWorld();
        var created = await world.Lobbies.Create(Guid.NewGuid());
        var unknown = created.Code == "ZZZZZZ" ? "YYYYYY" : "ZZZZZZ";

        (await world.Codes.Find(unknown)).Should().BeNull();
    }

    [Fact]
    public void Join_UnknownSession_ReturnsNotFound()
    {
        var world = new HostingTestWorld();

        world.Lobbies.JoinLobby(Guid.NewGuid(), Guid.NewGuid()).Should().Be(LobbyJoinStatus.NotFound);
    }

    [Fact]
    public void Join_MatchSession_ReturnsNotFound()
    {
        var world = new HostingTestWorld();
        var match = world.CreatePublicMatch();

        world.Lobbies.JoinLobby(match.Session.Id, Guid.NewGuid()).Should().Be(LobbyJoinStatus.NotFound);
    }

    [Fact]
    public async Task Join_FullOfReservations_ReturnsFull()
    {
        var world = new HostingTestWorld();
        var created = await world.Lobbies.Create(Guid.NewGuid());

        for (var i = 0; i < 3; i++)
            world.Lobbies.JoinLobby(created.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Ok);

        world.Lobbies.JoinLobby(created.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Full);
    }

    [Fact]
    public async Task Join_FullOfUsers_ReturnsFull_PlaceIsFreedWhenUserLeaves()
    {
        var world = new HostingTestWorld();
        var creator = Guid.NewGuid();

        var created = await world.Lobbies.Create(creator);
        var session = world.Sessions[created.SessionId];
        var users = new List<IUser> { world.Enter(created.SessionId, creator)! };

        for (var i = 0; i < 3; i++)
        {
            var guest = Guid.NewGuid();
            world.Lobbies.JoinLobby(created.SessionId, guest).Should().Be(LobbyJoinStatus.Ok);
            users.Add(world.Enter(created.SessionId, guest)!);
        }

        world.Lobbies.JoinLobby(created.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Full);

        world.Disconnect(session, users[3]);

        world.Lobbies.JoinLobby(created.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.Ok);
    }

    [Fact]
    public async Task LastUserLeft_SessionCloses_CodeIsReleased()
    {
        var world = new HostingTestWorld();
        var creator = Guid.NewGuid();

        var created = await world.Lobbies.Create(creator);
        var session = world.Sessions[created.SessionId];
        var user = world.Enter(created.SessionId, creator)!;

        world.Disconnect(session, user);

        session.Lifetime.IsTerminated.Should().BeTrue();
        (await world.Codes.Find(created.Code)).Should().BeNull();
        world.Lobbies.JoinLobby(created.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.NotFound);
    }

    [Fact]
    public async Task NobodyEntered_SessionClosesAfterJoinTimeout_CodeIsReleased()
    {
        var world = new HostingTestWorld();

        var created = await world.Lobbies.Create(Guid.NewGuid());
        var session = world.Sessions[created.SessionId];

        world.Time.Advance(HostingTestWorld.JoinTimeout);
        session.Queue.Drain();

        session.Lifetime.IsTerminated.Should().BeTrue();
        world.Codes.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Join_ClosedSessionBeforeItsLifetimeIsTerminated_ReturnsNotFound()
    {
        var world = new HostingTestWorld();
        var creator = Guid.NewGuid();

        var created = await world.Lobbies.Create(creator);
        var session = world.Sessions[created.SessionId];
        var user = world.Enter(created.SessionId, creator)!;

        // The user is removed, the termination of the session is still waiting in its queue:
        // the code is still registered, the session is still in the collection.
        session.Queue.Enqueue(user.Lifetime.Terminate);
        session.Queue.RunNext();

        session.Lifetime.IsTerminated.Should().BeFalse();
        (await world.Codes.Find(created.Code)).Should().NotBeNull();

        world.Lobbies.JoinLobby(created.SessionId, Guid.NewGuid()).Should().Be(LobbyJoinStatus.NotFound);
    }

    [Fact]
    public async Task ReleasedCode_CanBeGivenToAnotherLobby()
    {
        var world = new HostingTestWorld();
        world.Codes.SetGenerator(() => "AAAAAA");
        var creator = Guid.NewGuid();

        var first = await world.Lobbies.Create(creator);
        var user = world.Enter(first.SessionId, creator)!;
        world.Disconnect(world.Sessions[first.SessionId], user);

        var second = await world.Lobbies.Create(Guid.NewGuid());

        second.Code.Should().Be(first.Code);
        (await world.Codes.Find(second.Code))!.SessionId.Should().Be(second.SessionId);
    }

    [Fact]
    public async Task LobbyService_HoldsCodeAndLeaderIndex_LeaderPassesToNextUser()
    {
        var world = new HostingTestWorld();
        var creator = Guid.NewGuid();
        var guest = Guid.NewGuid();

        var created = await world.Lobbies.Create(creator);
        var session = world.Sessions[created.SessionId];

        var service = session.LobbyService();

        service.Read<LobbyState>(SharedLobbyService.LobbyPropertyId).Code.Should().Be(created.Code);
        service.Read<LobbyState>(SharedLobbyService.LobbyPropertyId).OwnerIndex.Should().Be(0);

        var first = world.Enter(created.SessionId, creator)!;
        world.Lobbies.JoinLobby(created.SessionId, guest).Should().Be(LobbyJoinStatus.Ok);
        var second = world.Enter(created.SessionId, guest)!;

        service.Read<LobbyState>(SharedLobbyService.LobbyPropertyId).OwnerIndex.Should().Be(first.Index);

        world.Disconnect(session, first);

        service.Read<LobbyState>(SharedLobbyService.LobbyPropertyId).OwnerIndex.Should().Be(second.Index);
        service.Read<LobbyState>(SharedLobbyService.LobbyPropertyId).Code.Should().Be(created.Code);
    }

    [Fact]
    public async Task Create_FactoryBuildsAnotherSession_Throws_CodeIsReleased()
    {
        var world = new HostingTestWorld();
        var built = world.CreatePublicMatch().Session;
        var factory = Substitute.For<ILobbyFactory>();
        factory.Create(Arg.Any<LobbyCreation>()).Returns(built);

        var lobbies = Lobbies(world, factory);

        var create = () => lobbies.Create(Guid.NewGuid());

        await create.Should().ThrowAsync<InvalidOperationException>();
        world.Codes.Entries.Should().BeEmpty();
    }

    // A factory that gives the lobby no place for its creator.
    [Fact]
    public async Task Create_LobbyRefusesItsCreator_Throws()
    {
        var world = new HostingTestWorld();
        var factory = Substitute.For<ILobbyFactory>();

        factory.Create(Arg.Any<LobbyCreation>()).Returns(call => {
            return new TestSession(new SessionCreateOptions
            {
                Id = call.Arg<LobbyCreation>().Id,
                Type = SessionType.Lobby,
                ExpectedUsers = 0,
                MaxUsers = 0,
                IsPrivate = true,
                Time = world.Time
            }).Session;
        });

        var lobbies = Lobbies(world, factory);

        var create = () => lobbies.Create(Guid.NewGuid());

        await create.Should().ThrowAsync<InvalidOperationException>();
    }

    private static LobbySessions Lobbies(HostingTestWorld world, ILobbyFactory factory)
    {
        return new LobbySessions(
            factory,
            world.Collection,
            world.Codes,
            world.Leases,
            world.EntryPolicy,
            world.Options,
            world.Discovery,
            NullLogger<LobbySessions>.Instance);
    }

    [Fact]
    public void LobbyService_WithoutCode_Throws()
    {
        var world = new HostingTestWorld();
        var session = world.CreatePublicMatch();

        var create = () => new LobbyService(
            session.Owner,
            session.Users,
            session.Admission,
            world,
            world.Leases,
            session.Queue,
            world.Time,
            new LobbySessionInfo
            {
                SessionId = session.Session.Id,
                Code = string.Empty,
                MaxUsers = 4
            },
            NullLogger<LobbyService>.Instance);

        create.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SessionContainer_OfALobby_HasTheLobbyService()
    {
        var lifetime = new Lifetime();

        var options = new SessionCreateOptions
        {
            Type = SessionType.Lobby,
            ExpectedUsers = 0,
            MaxUsers = 4,
            IsPrivate = true
        };

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(Substitute.For<ILobbyMatches>());
        services.AddSingleton(Substitute.For<ILobbyListings>());

        services.AddSessionServices(new SessionContainerData
        {
            Id = options.Id,
            Lifetime = lifetime,
            ExpectedUsers = options.ExpectedUsers
        }, options);

        services.AddLobbyService(new LobbySessionInfo
        {
            SessionId = options.Id,
            Code = "AAAAAA",
            MaxUsers = 4
        });

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IService>().Select(service => service.Key).Should().Equal(SharedLobbyService.Key);

        lifetime.Terminate();
    }

    [Fact]
    public void LobbyCode_Normalize_TrimsAndUppercases_IsValid_ChecksLengthAndAlphabet()
    {
        LobbyCode.Normalize("  ab2c9z ").Should().Be("AB2C9Z");
        LobbyCode.IsValid("AB2C9Z").Should().BeTrue();

        LobbyCode.IsValid("AB2C9").Should().BeFalse();
        LobbyCode.IsValid("AB2C9ZZ").Should().BeFalse();
        LobbyCode.IsValid("AB2C9I").Should().BeFalse();
        LobbyCode.IsValid("AB2C9O").Should().BeFalse();
        LobbyCode.IsValid("AB2C90").Should().BeFalse();
        LobbyCode.IsValid("AB2C91").Should().BeFalse();
        LobbyCode.IsValid("ab2c9z").Should().BeFalse();
        LobbyCode.IsValid("").Should().BeFalse();

        LobbyCode.Alphabet.Should().HaveLength(32);
        LobbyCode.Alphabet.Distinct().Should().HaveCount(32);
    }

    // The collection remembers when a session was added, not when it is asked.
    [Fact]
    public async Task SessionsCollection_CreatedAt_IsTheMomentOfCreation_NotOfTheRequest()
    {
        var world = new HostingTestWorld();

        var firstCreatedAt = world.Time.GetUtcNow().UtcDateTime;
        var first = await world.Lobbies.Create(Guid.NewGuid());

        world.Time.Advance(TimeSpan.FromSeconds(10));

        var secondCreatedAt = world.Time.GetUtcNow().UtcDateTime;
        var second = await world.Lobbies.Create(Guid.NewGuid());

        world.Time.Advance(TimeSpan.FromSeconds(10));

        world.Collection.Entries.Should().HaveCount(2);
        world.Collection.TryGetCreatedAt(first.SessionId, out var createdAt).Should().BeTrue();
        createdAt.Should().Be(firstCreatedAt);
        world.Collection.TryGetCreatedAt(second.SessionId, out createdAt).Should().BeTrue();
        createdAt.Should().Be(secondCreatedAt);

        world.Collection.TryGetCreatedAt(Guid.NewGuid(), out _).Should().BeFalse();
    }

    // The code is registered before the session exists: a factory that fails leaves no code behind.
    [Fact]
    public async Task Create_FactoryFails_CodeIsReleased()
    {
        var world = new HostingTestWorld();
        world.FailLobbyCreation(new InvalidOperationException("The game has failed to build the lobby"));

        var create = () => world.Lobbies.Create(Guid.NewGuid());

        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("The game has failed to build the lobby");
        world.Codes.Entries.Should().BeEmpty();
    }
}
