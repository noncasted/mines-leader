using FluentAssertions;
using Game.Session;
using Shared;
using Xunit;
using ErrorCode = Shared.ErrorCode;

namespace Framework.Tests.Session;

// Entity ids are created in the range of their creator and never reused; the backend builds its own entities in
// range 0. Events and batched property writes of an object that is gone are dropped, of an object that never existed
// fail; a service event goes only to its subscribers.
public class SessionEntityIdsTests
{
    [Fact]
    public void SharedEntityIds_RangeOfLastUser_FitsInt()
    {
        SharedEntityIds.Create(SharedEntityIds.MaxUserIndex, SharedEntityIds.Stride - 1).Should().Be(int.MaxValue);
        SharedEntityIds.Create(1, 1).Should().Be(SharedEntityIds.Stride + 1);
        SharedEntityIds.GetUserIndex(SharedEntityIds.Create(7, 5)).Should().Be(7);
    }

    [Fact]
    public void SharedEntityIds_CounterOutOfRange_Throws()
    {
        var past = () => SharedEntityIds.Create(1, SharedEntityIds.Stride);
        var zero = () => SharedEntityIds.Create(1, 0);
        var user = () => SharedEntityIds.Create(SharedEntityIds.MaxUserIndex + 1, 1);
        var backendZero = () => SharedEntityIds.CreateBackend(0);
        var backendPast = () => SharedEntityIds.CreateBackend(SharedEntityIds.Stride);

        past.Should().Throw<ArgumentOutOfRangeException>();
        zero.Should().Throw<ArgumentOutOfRangeException>();
        user.Should().Throw<ArgumentOutOfRangeException>();
        backendZero.Should().Throw<ArgumentOutOfRangeException>();
        backendPast.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SharedEntityIds_BackendRange_IsRangeZero_OfNoUser()
    {
        var id = SharedEntityIds.CreateBackend(5);

        id.Should().Be(5);
        SharedEntityIds.IsBackendIssued(id).Should().BeTrue();
        SharedEntityIds.GetUserIndex(id).Should().Be(0);
        SharedEntityIds.IsInRangeOf(id, 1).Should().BeFalse();

        SharedEntityIds.IsBackendIssued(SharedEntityIds.Create(1, 5)).Should().BeFalse();
        SharedEntityIds.IsBackendIssued(0).Should().BeFalse();
        SharedEntityIds.IsBackendIssued(Service.GetId("Chat")).Should().BeFalse();
    }

    [Fact]
    public void SharedEntityIds_ServiceId_IsInNoUserRange()
    {
        var serviceId = Service.GetId("Chat");

        SharedEntityIds.IsInRangeOf(serviceId, SharedEntityIds.GetUserIndex(serviceId)).Should().BeFalse();
        SharedEntityIds.IsInRangeOf(SharedEntityIds.Stride * 2, 2).Should().BeFalse("counter 0 is not an id");
    }

    [Fact]
    public void SessionUsers_PastMaxUserIndex_Throws()
    {
        var users = new SessionUsers();

        for (var index = 1; index <= SharedEntityIds.MaxUserIndex; index++)
            users.GetNextIndex().Should().Be(index);

        var next = () => users.GetNextIndex();

        next.Should().Throw<InvalidOperationException>().WithMessage("*user indices*");
    }

    [Fact]
    public void Create_InOwnRange_SendsOverviewToOthers()
    {
        var world = new IdsWorld();
        var creator = world.Join();
        var other = world.Join();

        var response = world.Create(creator, SharedEntityIds.Create(creator.Index, 1));

        response.EntityId.Should().Be(SharedEntityIds.Create(creator.Index, 1));
        TestSession.Sent<SharedSessionEntity.CreatedOverview>(other).Single().EntityId.Should().Be(response.EntityId);
        TestSession.Sent(creator).Should().BeEmpty();
        creator.Seat.EntityCounter.Should().Be(1);
    }

    [Fact]
    public void Create_SessionEntityInOwnRange_IsAccepted()
    {
        var world = new IdsWorld();
        var creator = world.Join();

        var response = world.Create(creator, SharedEntityIds.Create(creator.Index, 3), sessionEntity: true);

        world.Session.Entities.Entries[response.EntityId].OwnerIndex.Should().Be(Entity.SessionOwnerIndex);
    }

    [Fact]
    public void Create_InRangeOfAnotherUser_IsForbidden()
    {
        var world = new IdsWorld();
        var first = world.Join();
        var second = world.Join();

        var create = () => world.Create(first, SharedEntityIds.Create(second.Index, 1));

        create.Should().Throw<SessionRequestException>()
              .Where(e => e.Code == ErrorCode.Forbidden)
              .WithMessage("*out of its id range*");

        world.Session.Objects.Entries.Should().BeEmpty();
        TestSession.Sent(second).Should().BeEmpty();
    }

    // Range 0 belongs to the backend: a client can't take the id of an entity the backend builds.
    [Fact]
    public void Create_InBackendRange_IsForbidden_BackendStillBuildsIt()
    {
        var world = new IdsWorld();
        var user = world.Join();

        var create = () => world.Create(user, SharedEntityIds.CreateBackend(1));

        create.Should().Throw<SessionRequestException>().Where(e => e.Code == ErrorCode.Forbidden);

        var built = world.Build(user);
        built.Id.Should().Be(SharedEntityIds.CreateBackend(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10001)]
    public void Create_OldOrInvalidId_IsForbidden(int id)
    {
        var world = new IdsWorld();
        var user = world.Join();

        var create = () => world.Create(user, id);

        create.Should().Throw<SessionRequestException>().Where(e => e.Code == ErrorCode.Forbidden);
    }

    [Fact]
    public void Create_LiveId_IsForbidden()
    {
        var world = new IdsWorld();
        var user = world.Join();
        var id = SharedEntityIds.Create(user.Index, 1);
        world.Create(user, id);

        var again = () => world.Create(user, id);

        again.Should().Throw<SessionRequestException>().WithMessage("*already used*");
    }

    [Fact]
    public void Create_DestroyedId_IsForbidden()
    {
        var world = new IdsWorld();
        var user = world.Join();
        var id = SharedEntityIds.Create(user.Index, 1);
        world.Create(user, id);
        world.Session.Entities.Entries[id].Destroy();

        var again = () => world.Create(user, id);

        again.Should().Throw<SessionRequestException>().WithMessage("*already used*");
    }

    // An entity built by the backend for a seat: its id is in range 0, its overview goes to every user (the owner
    // too - its client gets it as a remote entity), it lives while the seat does.
    [Fact]
    public void Build_IdInBackendRange_OverviewToAllUsers_OwnedBySeat()
    {
        var world = new IdsWorld();
        var owner = world.Join();
        var other = world.Join();

        var first = world.Build(owner);
        var second = world.Build(other);

        first.Id.Should().Be(SharedEntityIds.CreateBackend(1));
        second.Id.Should().Be(SharedEntityIds.CreateBackend(2));
        SharedEntityIds.IsBackendIssued(first.Id).Should().BeTrue();
        SharedEntityIds.GetUserIndex(first.Id).Should().Be(0);

        first.Owner.Should().BeSameAs(owner.Seat);
        first.OwnerIndex.Should().Be(owner.Index);
        world.Session.Objects.Entries[first.Id].Should().BeSameAs(first);
        owner.Seat.EntityCounter.Should().Be(0, "the backend range is not the range of the seat");

        foreach (var user in new[] { owner, other })
        {
            TestSession.Sent<SharedSessionEntity.CreatedOverview>(user).Select(overview => overview.EntityId).Should()
                       .Equal(first.Id, second.Id);
        }

        TestSession.Sent<SharedSessionEntity.CreatedOverview>(owner).First().OwnerId.Should().Be(owner.Index);
    }

    [Fact]
    public void Build_WithProperty_OverviewCarriesIt_UpdateGoesToAll()
    {
        var world = new IdsWorld();
        var owner = world.Join();
        var other = world.Join();

        var builder = world.Session.EntityFactory.Create(owner.Seat);
        var property = builder.AddProperty<int>(4);
        var entity = builder.WithPayload(new TestPayload()).Build();

        TestSession.Sent<SharedSessionEntity.CreatedOverview>(other).Single().Properties.Should()
                   .ContainSingle(update => update.PropertyId == 4);

        property.Set(3);

        foreach (var user in new[] { owner, other })
        {
            TestSession.Sent<SharedSessionObject.PropertyUpdate>(user).Should()
                       .ContainSingle(update => update.ObjectId == entity.Id && update.PropertyId == 4);
        }
    }

    [Fact]
    public void Build_WithoutPayload_Throws()
    {
        var world = new IdsWorld();
        var owner = world.Join();

        var build = () => world.Session.EntityFactory.Create(owner.Seat).Build();

        build.Should().Throw<InvalidOperationException>().WithMessage("*without a payload*");
    }

    [Fact]
    public void Build_SeatEnds_EntityEnds()
    {
        var world = new IdsWorld();
        var owner = world.Join();
        var other = world.Join();
        var entity = world.Build(owner);

        world.Session.Leave(owner);

        entity.Lifetime.IsTerminated.Should().BeTrue();
        world.Session.Entities.Entries.Should().NotContainKey(entity.Id);
        world.Session.Objects.IsRemoved(entity.Id).Should().BeTrue();
        other.Lifetime.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void Event_OfLiveEntity_GoesToOthers()
    {
        var world = new IdsWorld();
        var sender = world.Join();
        var other = world.Join();
        var id = world.Create(sender, SharedEntityIds.Create(sender.Index, 1)).EntityId;
        world.Session.ClearSent();

        world.Event(sender, id);

        TestSession.Sent<SharedSessionObject.Event>(other).Single().ObjectId.Should().Be(id);
        TestSession.Sent(sender).Should().BeEmpty();
    }

    [Fact]
    public void Event_OfDestroyedEntity_IsDropped()
    {
        var world = new IdsWorld();
        var sender = world.Join();
        var other = world.Join();
        var id = world.Create(sender, SharedEntityIds.Create(sender.Index, 1)).EntityId;
        world.Session.Entities.Entries[id].Destroy();

        world.Event(other, id);

        TestSession.Sent<SharedSessionObject.Event>(sender).Should().BeEmpty();
    }

    [Fact]
    public void Event_OfNeverExistingObject_Throws()
    {
        var world = new IdsWorld();
        var sender = world.Join();

        var send = () => world.Event(sender, 4242);

        send.Should().Throw<InvalidOperationException>().WithMessage("*object 4242*does not exist*");
    }

    [Fact]
    public void Event_OfService_GoesOnlyToOtherSubscribers()
    {
        var world = new IdsWorld();
        var sender = world.Join();
        var subscriber = world.Join();
        var stranger = world.Join();

        var service = world.Session.ServiceFactory.GetOrCreate(new SharedSessionService.GetRequest
        {
            Key = "Chat",
            PropertiesIds = []
        });

        service.AddSubscriber(sender);
        service.AddSubscriber(subscriber);

        world.Event(sender, service.Id);

        TestSession.Sent<SharedSessionObject.Event>(subscriber).Single().ObjectId.Should().Be(service.Id);
        TestSession.Sent(stranger).Should().BeEmpty();
        TestSession.Sent(sender).Should().BeEmpty();
    }

    [Fact]
    public void SetProperties_AppliesEveryEntry_AndSkipsDestroyedObject()
    {
        var world = new IdsWorld();
        var writer = world.Join();
        var other = world.Join();
        var live = world.Create(writer, SharedEntityIds.Create(writer.Index, 1), propertyIds: [1, 2]).EntityId;
        var gone = world.Create(writer, SharedEntityIds.Create(writer.Index, 2), propertyIds: [1]).EntityId;
        world.Session.Entities.Entries[gone].Destroy();

        world.SetProperties(writer,
            (live, 1, [5]),
            (gone, 1, [6]),
            (live, 2, [7]));

        var updates = TestSession.Sent<SharedSessionObject.PropertyUpdate>(other);
        updates.Select(update => (update.ObjectId, update.PropertyId, update.Value[0], update.Version)).Should()
               .Equal((live, 1, (byte)5, 1), (live, 2, (byte)7, 1));
    }

    [Fact]
    public void SetProperties_FailedEntry_DoesNotStopTheOthers()
    {
        var world = new IdsWorld();
        var writer = world.Join();
        var other = world.Join();
        var live = world.Create(writer, SharedEntityIds.Create(writer.Index, 1), propertyIds: [1]).EntityId;

        world.SetProperties(writer,
            (4242, 1, [1]),
            (live, 99, [2]),
            (live, 1, [3]));

        TestSession.Sent<SharedSessionObject.PropertyUpdate>(other).Single().Value.Should().Equal(3);
    }

    // A started session of joined users; what the users got on their join is cleared.
    private class IdsWorld
    {
        public readonly TestSession Session = TestSession.Create();

        public IUser Join()
        {
            var user = Session.ReserveAndEnter();
            Session.ClearSent();
            return user;
        }

        public SharedSessionEntity.CreateResponse Create(
            IUser user,
            int id,
            bool sessionEntity = false,
            int[]? propertyIds = null)
        {
            var request = new SharedSessionEntity.CreateRequest
            {
                Id = id,
                Properties = (propertyIds ?? []).Select(propertyId => new SharedSessionObject.PropertyUpdate
                {
                    PropertyId = propertyId,
                    Value = []
                }).ToList(),
                Payload = sessionEntity == true ? new TestSessionPayload() : new TestPayload()
            };

            return (SharedSessionEntity.CreateResponse)Session.Request(user, request);
        }

        // An entity the backend builds for the seat of the user.
        public IEntity Build(IUser owner)
        {
            return Session.EntityFactory.Create(owner.Seat)
                          .WithPayload(new TestPayload())
                          .Build();
        }

        public void Event(IUser user, int objectId)
        {
            Session.Execute(user, new SharedSessionObject.Event
            {
                ObjectId = objectId,
                Value = [1]
            });
        }

        public void SetProperties(IUser user, params (int ObjectId, int PropertyId, byte[] Value)[] entries)
        {
            Session.Execute(user, new SharedSessionObject.SetProperties
            {
                Entries = entries.Select(entry => new SharedSessionObject.SetProperty
                {
                    ObjectId = entry.ObjectId,
                    PropertyId = entry.PropertyId,
                    Value = entry.Value
                }).ToList()
            });
        }
    }
}
