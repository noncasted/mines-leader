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

// A lobby session of HostingTestWorld with its users connected: the start of the lobby is driven the way the client
// of the leader and the clock of the server do it.
public class TestLobby
{
    private TestLobby(HostingTestWorld world, TestSession session, IReadOnlyList<IUser> users)
    {
        World = world;
        Session = session;
        Users = users;
    }

    public readonly HostingTestWorld World;
    public readonly TestSession Session;

    // The creator (the leader) first, then the guests in the order they entered.
    public readonly IReadOnlyList<IUser> Users;

    public LobbyService Service => Session.LobbyService();
    public LobbyState State => Service.Read<LobbyState>(SharedLobbyService.LobbyPropertyId);
    public LobbyStartState Start => Service.Read<LobbyStartState>(SharedLobbyService.StartPropertyId);
    public LobbyCountdownState Countdown => Service.Read<LobbyCountdownState>(SharedLobbyService.CountdownPropertyId);
    public LobbyGameState Game => Service.Read<LobbyGameState>(SharedLobbyService.GamePropertyId);
    public string Code => State.Code;

    public static Task<TestLobby> Create(int guests)
    {
        return Create(new HostingTestWorld(), guests);
    }

    public static async Task<TestLobby> Create(HostingTestWorld world, int guests)
    {
        var creator = Guid.NewGuid();

        var created = await world.Lobbies.Create(creator);
        var users = new List<IUser> { world.Enter(created.SessionId, creator)! };

        for (var i = 0; i < guests; i++)
        {
            var guest = Guid.NewGuid();

            world.Lobbies.Join(created.SessionId, Guid.Empty, created.Code, guest).Status.Should().Be(LobbyJoinStatus.Ok);
            users.Add(world.Enter(created.SessionId, guest)!);
        }

        return new TestLobby(world, world.Sessions[created.SessionId], users);
    }

    // A write of the leader's client: the real SetPropertyCommand in the session queue.
    public void WriteStart(bool isStarting)
    {
        WriteStart(isStarting, Session.Owner.Owner.Value!);
    }

    public void WriteVisibility(bool isOpen)
    {
        WriteVisibility(isOpen, Session.Owner.Owner.Value!);
    }

    public void WriteVisibility(bool isOpen, IUser writer)
    {
        Write(writer, SharedLobbyService.VisibilityPropertyId, new LobbyVisibilityState
        {
            IsOpen = isOpen
        });
    }

    public void WriteStart(bool isStarting, IUser writer)
    {
        Write(writer, SharedLobbyService.StartPropertyId, new LobbyStartState
        {
            IsStarting = isStarting
        });
    }

    public void Write<T>(IUser writer, int propertyId, T value)
    {
        EnqueueWrite(writer, propertyId, value);
        Session.Queue.Drain();
    }

    // Only queued: the test decides what runs after it.
    public void EnqueueWrite<T>(IUser writer, int propertyId, T value)
    {
        var context = new SharedSessionObject.SetProperty
        {
            ObjectId = Service.Id,
            PropertyId = propertyId,
            Value = MemoryPackSerializer.Serialize(value)
        };

        Session.Queue.Enqueue(() => Session.Execute(writer, context));
    }

    // In steps of 100 ms: a countdown step schedules the next one when it runs.
    public void Advance(TimeSpan time)
    {
        var step = TimeSpan.FromMilliseconds(100);

        for (var passed = TimeSpan.Zero; passed < time; passed += step)
        {
            World.Time.Advance(step);
            Session.Queue.Drain();
        }
    }

    public void RunCountdown()
    {
        WriteStart(true);
        Advance(LobbyService.CountdownStep * LobbyCountdownState.Seconds);

        Game.SessionId.Should().NotBe(Guid.Empty);
    }
}
