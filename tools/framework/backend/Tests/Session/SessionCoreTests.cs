using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.Session;
using Internal;
using Shared;
using Xunit;

namespace Framework.Tests.Session;

// SessionCore: the session of a host client (P2P match, offline) without a container, on the same parts and commands
// as the backend session. Users are introduced to each other, entities and property writes go to the others, a write
// of a restricted property by another user is dropped, a leave ends the seat for the others.
public class SessionCoreTests : LifetimeTest
{
    [Fact]
    public void Connect_UsersIntroducedToEachOther()
    {
        var core = Create();
        var first = new RecordingConnection();
        var second = new RecordingConnection();

        var firstUser = core.Connect(core.Lifetime, Guid.NewGuid(), first);
        var secondUser = core.Connect(core.Lifetime, Guid.NewGuid(), second);

        firstUser.Index.Should().Be(1);
        secondUser.Index.Should().Be(2);

        first.OneWay.OfType<SharedSessionPlayer.LocalUpdate>().Single().Index.Should().Be(1);
        first.OneWay.OfType<SharedSessionPlayer.RemoteUpdate>().Single().Index.Should().Be(2);
        second.OneWay.OfType<SharedSessionPlayer.LocalUpdate>().Single().Index.Should().Be(2);
        second.OneWay.OfType<SharedSessionPlayer.RemoteUpdate>().Single().Index.Should().Be(1);

        core.Owner.Owner.Value.Should().Be(firstUser);
    }

    [Fact]
    public void EntityCreate_GoesToOthers_PropertyWriteRelayedWithVersion()
    {
        var core = Create();
        var first = new RecordingConnection();
        var second = new RecordingConnection();
        var firstUser = core.Connect(core.Lifetime, Guid.NewGuid(), first);
        core.Connect(core.Lifetime, Guid.NewGuid(), second);

        var id = SharedEntityIds.Create(firstUser.Index, 1);

        core.OnRequest(firstUser, new SharedSessionEntity.CreateRequest
        {
            Id = id,
            Payload = new TestPayload(),
            Properties = new[] { new SharedSessionObject.PropertyUpdate { PropertyId = 5, Value = new byte[] { 1 } } }
        }, 10);

        first.Responses.Should().ContainSingle(r => r.RequestId == 10 && r.Context is SharedSessionEntity.CreateResponse);
        second.OneWay.OfType<SharedSessionEntity.CreatedOverview>().Single().EntityId.Should().Be(id);

        core.OnOneWay(firstUser, new SharedSessionObject.SetProperty { ObjectId = id, PropertyId = 5, Value = new byte[] { 2 } });

        var update = second.OneWay.OfType<SharedSessionObject.PropertyUpdate>().Single();
        update.Value.Should().Equal(2);
        update.Version.Should().Be(1);
        first.OneWay.OfType<SharedSessionObject.PropertyUpdate>().Should().BeEmpty();
    }

    [Fact]
    public void ServiceProperty_RestrictedWriter_OtherUserWriteDropped()
    {
        var core = Create(start: false);
        var service = new Service("Game");
        // A plain type of the protocol stands for the state of a game service.
        var property = new ValueProperty<SharedSessionLatency.Ping>(1);
        property.RestrictWriter(user => user.Index == 1);
        service.BindProperty(property);
        core.AddService(service);
        core.Start();

        var first = new RecordingConnection();
        var second = new RecordingConnection();
        var firstUser = core.Connect(core.Lifetime, Guid.NewGuid(), first);
        var secondUser = core.Connect(core.Lifetime, Guid.NewGuid(), second);

        var request = new SharedSessionService.GetRequest { Key = "Game", PropertiesIds = new[] { 1 } };
        core.OnRequest(firstUser, request, 1);
        core.OnRequest(secondUser, request, 1);

        var value = MemoryPack.MemoryPackSerializer.Serialize(new SharedSessionLatency.Ping { Id = 7 });

        core.OnOneWay(secondUser, new SharedSessionObject.SetProperty { ObjectId = service.Id, PropertyId = 1, Value = value });
        property.Value.Id.Should().Be(0);
        first.OneWay.OfType<SharedSessionObject.PropertyUpdate>().Should().BeEmpty();

        core.OnOneWay(firstUser, new SharedSessionObject.SetProperty { ObjectId = service.Id, PropertyId = 1, Value = value });
        property.Value.Id.Should().Be(7);
        second.OneWay.OfType<SharedSessionObject.PropertyUpdate>().Single().Version.Should().Be(1);
    }

    [Fact]
    public void Leave_SeatEnded_OthersToldAndEntitiesDestroyed_OwnerPasses()
    {
        var core = Create();
        var first = new RecordingConnection();
        var second = new RecordingConnection();
        var firstLifetime = core.Lifetime.Child();
        var firstUser = core.Connect(firstLifetime, Guid.NewGuid(), first);
        var secondUser = core.Connect(core.Lifetime, Guid.NewGuid(), second);

        var id = SharedEntityIds.Create(firstUser.Index, 1);
        core.OnRequest(firstUser, new SharedSessionEntity.CreateRequest
        {
            Id = id,
            Payload = new TestPayload(),
            Properties = Array.Empty<SharedSessionObject.PropertyUpdate>()
        }, 1);

        firstLifetime.Terminate();

        second.OneWay.OfType<SharedSessionPlayer.RemoteDisconnect>().Single().Index.Should().Be(1);
        core.Entities.Entries.Should().NotContainKey(id);
        core.Users.Should().Equal(secondUser);
        core.Owner.Owner.Value.Should().Be(secondUser);
    }

    // A and C are in the session, A loses its connection (its seat waits), a new client enters: it gets the entities of
    // A in the overview, so it has to know their owner first, as an away user. Without it the client throws on the
    // owner of the entity and has no entity of A until the end of the match.
    [Fact]
    public void Introduce_AwaySeat_KnownAsAwayBeforeItsEntities()
    {
        var core = Create();
        var away = core.Connect(core.Lifetime, Guid.NewGuid(), new RecordingConnection());
        core.Connect(core.Lifetime, Guid.NewGuid(), new RecordingConnection());

        var id = SharedEntityIds.Create(away.Index, 1);

        core.OnRequest(away, new SharedSessionEntity.CreateRequest
        {
            Id = id,
            Payload = new TestPayload(),
            Properties = Array.Empty<SharedSessionObject.PropertyUpdate>()
        }, 1);

        core.Seats.Detach(away);

        var next = new RecordingConnection();
        core.Connect(core.Lifetime, Guid.NewGuid(), next);

        var messages = next.OneWay;
        var known = messages.FindIndex(m => m is SharedSessionPlayer.RemoteUpdate update && update.Index == away.Index);
        var isAway = messages.FindIndex(m => m is SharedSessionPlayer.RemoteAway update && update.Index == away.Index);
        var entity = messages.FindIndex(m => m is SharedSessionEntity.CreatedOverview overview && overview.EntityId == id);

        known.Should().BeGreaterThanOrEqualTo(0, "the new client has to know the owner of the entity");
        isAway.Should().BeGreaterThan(known);
        entity.Should().BeGreaterThan(isAway);
    }

    // The connected users are told about the others as before: no RemoteAway for them.
    [Fact]
    public void Introduce_ConnectedSeat_NotAway()
    {
        var core = Create();
        core.Connect(core.Lifetime, Guid.NewGuid(), new RecordingConnection());

        var next = new RecordingConnection();
        core.Connect(core.Lifetime, Guid.NewGuid(), next);

        next.OneWay.OfType<SharedSessionPlayer.RemoteAway>().Should().BeEmpty();
        next.OneWay.OfType<SharedSessionPlayer.RemoteUpdate>().Should().ContainSingle();
    }

    // A reconnectable host: a lost connection keeps the seat and its entities, the others see the user away, and the
    // user that connects again gets its seat back with its entities to restore.
    [Fact]
    public void Loss_Reconnectable_SeatWaits_ReturnRestoresIt()
    {
        var core = Create(isReconnectable: true);
        var other = new RecordingConnection();
        var userId = Guid.NewGuid();
        var lostLifetime = core.Lifetime.Child();
        var lost = core.Connect(lostLifetime, userId, new RecordingConnection());
        core.Connect(core.Lifetime, Guid.NewGuid(), other);

        var id = SharedEntityIds.Create(lost.Index, 1);
        core.OnRequest(lost, new SharedSessionEntity.CreateRequest
        {
            Id = id,
            Payload = new TestPayload(),
            Properties = Array.Empty<SharedSessionObject.PropertyUpdate>()
        }, 1);

        IReadOnlyLifetime wait = null;
        core.SeatAway.Advise(core.Lifetime, (_, lifetime) => wait = lifetime);

        lostLifetime.Terminate();

        other.OneWay.OfType<SharedSessionPlayer.RemoteAway>().Single().Index.Should().Be(lost.Index);
        other.OneWay.OfType<SharedSessionPlayer.RemoteDisconnect>().Should().BeEmpty();
        core.Entities.Entries.Should().ContainKey(id);
        wait.IsTerminated.Should().BeFalse();

        var returned = new RecordingConnection();
        var user = core.Connect(core.Lifetime, userId, returned);

        user.Index.Should().Be(lost.Index);
        wait.IsTerminated.Should().BeTrue();

        var local = returned.OneWay.OfType<SharedSessionPlayer.LocalUpdate>().Single();
        local.IsReturn.Should().BeTrue();
        local.SeatEntities.Should().Be(1);
        other.OneWay.OfType<SharedSessionPlayer.RemoteUpdate>().Last().Index.Should().Be(lost.Index);
    }

    [Fact]
    public void Loss_Reconnectable_Expired_SeatEnded()
    {
        var core = Create(isReconnectable: true);
        var other = new RecordingConnection();
        var lostLifetime = core.Lifetime.Child();
        var lost = core.Connect(lostLifetime, Guid.NewGuid(), new RecordingConnection());
        core.Connect(core.Lifetime, Guid.NewGuid(), other);

        ISessionSeat away = null;
        IReadOnlyLifetime wait = null;
        core.SeatAway.Advise(core.Lifetime, (seat, lifetime) => {
            away = seat;
            wait = lifetime;
        });

        lostLifetime.Terminate();
        core.ExpireAway(away);

        other.OneWay.OfType<SharedSessionPlayer.RemoteDisconnect>().Single().Index.Should().Be(lost.Index);
        wait.IsTerminated.Should().BeTrue();
        core.Seats.Entries.Should().NotContainKey(lost.BackendId);

        var act = () => core.ExpireAway(away);
        act.Should().Throw<InvalidOperationException>();
    }

    // A leave is not a loss, a reconnectable host included.
    [Fact]
    public void Leave_Reconnectable_SeatEnded()
    {
        var core = Create(isReconnectable: true);
        var other = new RecordingConnection();
        var leavingLifetime = core.Lifetime.Child();
        var leaving = core.Connect(leavingLifetime, Guid.NewGuid(), new RecordingConnection());
        core.Connect(core.Lifetime, Guid.NewGuid(), other);

        core.OnOneWay(leaving, new SharedSessionPlayer.Leave());
        leavingLifetime.Terminate();

        other.OneWay.OfType<SharedSessionPlayer.RemoteAway>().Should().BeEmpty();
        other.OneWay.OfType<SharedSessionPlayer.RemoteDisconnect>().Single().Index.Should().Be(leaving.Index);
    }

    // The user returns before the host has noticed the loss: the previous connection is dropped and ends silently.
    [Fact]
    public void Return_BeforeLossNoticed_StaleConnectionDropped()
    {
        var core = Create(isReconnectable: true);
        var other = new RecordingConnection();
        var userId = Guid.NewGuid();
        var staleLifetime = core.Lifetime.Child();
        var stale = new RecordingConnection { OnDisconnect = staleLifetime.Terminate };
        core.Connect(staleLifetime, userId, stale);
        core.Connect(core.Lifetime, Guid.NewGuid(), other);

        var user = core.Connect(core.Lifetime, userId, new RecordingConnection());

        staleLifetime.IsTerminated.Should().BeTrue();
        user.Seat.Connected.Should().Be(user);
        core.Users.Should().HaveCount(2);
        other.OneWay.OfType<SharedSessionPlayer.RemoteAway>().Should().BeEmpty();
        other.OneWay.OfType<SharedSessionPlayer.RemoteDisconnect>().Should().BeEmpty();
    }

    [Fact]
    public void Ping_Answered()
    {
        var core = Create();
        var connection = new RecordingConnection();
        var user = core.Connect(core.Lifetime, Guid.NewGuid(), connection);

        core.OnOneWay(user, new SharedSessionLatency.Ping { Id = 3 });

        connection.OneWay.OfType<SharedSessionLatency.Pong>().Single().Id.Should().Be(3);
    }

    private SessionCore Create(bool start = true, bool isReconnectable = false)
    {
        var core = new SessionCore(Guid.NewGuid(), SessionType.Match, new Lifetime(TestLifetime), new NullSessionLog(), isReconnectable);

        if (start == true)
            core.Start();

        return core;
    }

    private class RecordingConnection : IUserConnection
    {
        public readonly List<INetworkContext> OneWay = new();
        public readonly List<(INetworkContext Context, int RequestId)> Responses = new();

        public void Send(INetworkContext context)
        {
            OneWay.Add(context);
        }

        public void Respond(INetworkContext context, int requestId)
        {
            Responses.Add((context, requestId));
        }

        public Action OnDisconnect { get; init; }

        public void Disconnect()
        {
            OnDisconnect?.Invoke();
        }
    }
}

