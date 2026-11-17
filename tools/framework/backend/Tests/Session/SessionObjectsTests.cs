using Framework.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Internal;
using FluentAssertions;
using Game.Session;
using Shared;
using Xunit;

namespace Framework.Tests.Session;

// Objects of a session: the overview of an entity carries the versions of its properties, service ids are stable
// and negative, a write or destroy of an object that is already gone is dropped and of an object that never existed
// fails; a session entity outlives its creator.
public class SessionObjectsTests : LifetimeTest
{
    [Fact]
    public void EntityOverview_CarriesVersionOfEveryProperty()
    {
        var world = new ObjectsWorld();
        var creator = world.Join();
        var entity = world.AddEntity(creator, propertyIds: [1, 2]);

        entity.Properties[1].Update([1]);
        entity.Properties[1].Update([2]);
        entity.Properties[2].Update([3]);

        var overview = (SharedSessionEntity.CreatedOverview)entity.CreateOverview();

        overview.EntityId.Should().Be(entity.Id);
        overview.OwnerId.Should().Be(creator.Index);
        overview.Properties.Should().BeEquivalentTo(new[]
        {
            new { PropertyId = 1, Version = 2, Value = new byte[] { 2 } },
            new { PropertyId = 2, Version = 1, Value = new byte[] { 3 } }
        }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void EntityOverview_UnwrittenProperty_HasVersionZero()
    {
        var world = new ObjectsWorld();
        var creator = world.Join();
        var entity = world.AddEntity(creator, propertyIds: [1]);

        var overview = (SharedSessionEntity.CreatedOverview)entity.CreateOverview();

        overview.Properties.Single().Version.Should().Be(0);
    }

    [Fact]
    public void ServiceId_IsStableHashWithSignBit()
    {
        Service.GetId("Chat").Should().Be(Shared.StableHash.Get("Chat") | int.MinValue);
        new Service("Chat").Id.Should().Be(Service.GetId("Chat"));
    }

    [Fact]
    public void ServiceIds_AreNegative_AndDistinct()
    {
        var keys = new[] { "Chat", "Lobby", "Location", "Listing", "Score" };
        var ids = keys.Select(Service.GetId).ToList();

        ids.Should().OnlyContain(id => id < 0);
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ServiceFactory_CreatedService_HasNegativeId_AndIsInObjects()
    {
        var world = new ObjectsWorld();

        var service = world.Session.ServiceFactory.GetOrCreate(new SharedSessionService.GetRequest
        {
            Key = "Chat",
            PropertiesIds = [3]
        });

        service.Id.Should().Be(Service.GetId("Chat"));
        service.Id.Should().BeNegative();
        service.Properties.Keys.Should().Equal(3);
        world.Session.Objects.Entries[service.Id].Should().BeSameAs(service);

        world.Session.ServiceFactory.GetOrCreate(new SharedSessionService.GetRequest
        {
            Key = "Chat",
            PropertiesIds = []
        }).Should().BeSameAs(service);
    }

    // A property of a service goes only to its subscribers.
    [Fact]
    public void SetProperty_Service_GoesOnlyToOtherSubscribers()
    {
        var world = new ObjectsWorld();
        var writer = world.Join();
        var subscriber = world.Join();
        var stranger = world.Join();

        var service = world.Session.ServiceFactory.GetOrCreate(new SharedSessionService.GetRequest
        {
            Key = "Chat",
            PropertiesIds = [1]
        });

        service.AddSubscriber(writer);
        service.AddSubscriber(subscriber);

        world.SetProperty(writer, service.Id, 1, [7]);

        TestSession.Sent<SharedSessionObject.PropertyUpdate>(subscriber).Should()
                   .ContainSingle(update => update.ObjectId == service.Id && update.Version == 1);

        TestSession.Sent(stranger).Should().BeEmpty();
        TestSession.Sent(writer).Should().BeEmpty();
    }

    [Fact]
    public void SessionObjects_SecondObjectWithTakenId_Throws()
    {
        // Set up as ServiceFactory does before adding: a service that is not set up has an ended lifetime and
        // leaves the objects right away.
        var objects = new SessionObjects();
        var lifetime = new Lifetime(TestLifetime);
        var first = new Service("Chat");
        var second = new Service("Chat");
        first.Setup(lifetime);
        second.Setup(lifetime);
        objects.Add(first);

        var add = () => objects.Add(second);

        add.Should().Throw<InvalidOperationException>().WithMessage("*service Chat*");
    }

    [Fact]
    public void SessionObjects_RemovedObject_IsRemoved_NeverAddedIsNot()
    {
        var world = new ObjectsWorld();
        var creator = world.Join();
        var entity = world.AddEntity(creator, propertyIds: [1]);

        world.Session.Objects.IsRemoved(entity.Id).Should().BeFalse();

        entity.Destroy();

        world.Session.Objects.Entries.Should().NotContainKey(entity.Id);
        world.Session.Objects.IsRemoved(entity.Id).Should().BeTrue();
        world.Session.Objects.IsRemoved(entity.Id + 1).Should().BeFalse();
    }

    [Fact]
    public void SetProperty_LiveEntity_Applies_SendsToOthers()
    {
        var world = new ObjectsWorld();
        var writer = world.Join();
        var other = world.Join();
        var entity = world.AddEntity(writer, propertyIds: [1]);
        world.Session.ClearSent();

        world.SetProperty(writer, entity.Id, 1, [7]);

        entity.Properties[1].RawValue.Should().Equal(7);
        TestSession.Sent<SharedSessionObject.PropertyUpdate>(other).Should().ContainSingle(update =>
            update.ObjectId == entity.Id && update.PropertyId == 1 && update.Version == 1);
        TestSession.Sent(writer).Should().BeEmpty();
    }

    [Fact]
    public void SetProperty_DestroyedEntity_IsDropped()
    {
        var world = new ObjectsWorld();
        var writer = world.Join();
        var other = world.Join();
        var entity = world.AddEntity(writer, propertyIds: [1]);
        entity.Destroy();
        world.Session.ClearSent();

        var write = () => world.SetProperty(writer, entity.Id, 1, [7]);

        write.Should().NotThrow();
        TestSession.Sent(other).Should().BeEmpty();
    }

    // The owner left: its entities end with its seat, a write from the other user crossed that.
    [Fact]
    public void SetProperty_EntityOfUserThatLeft_IsDropped()
    {
        var world = new ObjectsWorld();
        var writer = world.Join();
        var owner = world.Join();
        var entity = world.AddEntity(owner, propertyIds: [1]);

        world.Session.Leave(owner);
        entity.Lifetime.IsTerminated.Should().BeTrue();

        var write = () => world.SetProperty(writer, entity.Id, 1, [7]);

        write.Should().NotThrow();
    }

    [Fact]
    public void SetProperty_UnknownObject_Throws()
    {
        var world = new ObjectsWorld();
        var writer = world.Join();

        var write = () => world.SetProperty(writer, 4242, 1, [7]);

        write.Should().Throw<InvalidOperationException>().WithMessage("*object 4242*does not exist*");
    }

    [Fact]
    public void SetProperty_UnknownPropertyOfLiveObject_Throws()
    {
        var world = new ObjectsWorld();
        var writer = world.Join();
        var entity = world.AddEntity(writer, propertyIds: [1]);

        var write = () => world.SetProperty(writer, entity.Id, 99, [7]);

        write.Should().Throw<InvalidOperationException>().WithMessage("*property 99*");
    }

    [Fact]
    public void Destroy_LiveEntity_Terminates_TellsOthers()
    {
        var world = new ObjectsWorld();
        var owner = world.Join();
        var other = world.Join();
        var entity = world.AddEntity(owner, propertyIds: [1]);
        world.Session.ClearSent();

        world.Destroy(owner, entity.Id);

        entity.Lifetime.IsTerminated.Should().BeTrue();
        world.Session.Entities.Entries.Should().NotContainKey(entity.Id);
        world.Session.Entities.ByUser[owner.Seat].Should().BeEmpty();
        TestSession.Sent<SharedSessionEntity.DestroyUpdate>(other).Should()
                   .ContainSingle(update => update.EntityId == entity.Id);
        TestSession.Sent(owner).Should().BeEmpty();
    }

    [Fact]
    public void Destroy_AlreadyDestroyedEntity_IsDropped()
    {
        var world = new ObjectsWorld();
        var owner = world.Join();
        var other = world.Join();

        // The game destroyed the session entity while a user was destroying it too.
        var entity = world.AddEntity(owner, propertyIds: [1], sessionEntity: true);
        entity.Destroy();
        world.Session.ClearSent();

        var destroy = () => world.Destroy(owner, entity.Id);

        destroy.Should().NotThrow();
        TestSession.Sent(other).Should().BeEmpty();
    }

    [Fact]
    public void Destroy_UnknownEntity_Throws()
    {
        var world = new ObjectsWorld();
        var owner = world.Join();

        var destroy = () => world.Destroy(owner, 4242);

        destroy.Should().Throw<InvalidOperationException>().WithMessage("*entity 4242*does not exist*");
    }

    // A session entity belongs to the session: its creator leaving does not destroy it, any user may destroy it.
    [Fact]
    public void SessionEntity_OutlivesItsCreator_OwnedBySession()
    {
        var world = new ObjectsWorld();
        var creator = world.Join();
        var other = world.Join();

        var entity = world.AddEntity(creator, propertyIds: [], sessionEntity: true);
        world.Session.Leave(creator);

        entity.Owner.Should().BeNull();
        entity.OwnerIndex.Should().Be(Entity.SessionOwnerIndex);
        entity.Lifetime.IsTerminated.Should().BeFalse();
        world.Session.Entities.Entries.Should().ContainKey(entity.Id);

        world.Destroy(other, entity.Id);

        entity.Lifetime.IsTerminated.Should().BeTrue();
    }

    // A user entity ends with the seat of its creator.
    [Fact]
    public void UserEntity_EndsWithSeatOfItsCreator()
    {
        var world = new ObjectsWorld();
        var creator = world.Join();
        world.Join();

        var entity = world.AddEntity(creator, propertyIds: []);
        entity.Owner.Should().BeSameAs(creator.Seat);
        world.Session.Entities.ByUser[creator.Seat].Should().Equal(entity);

        world.Session.Leave(creator);

        entity.Lifetime.IsTerminated.Should().BeTrue();
        world.Session.Entities.Entries.Should().NotContainKey(entity.Id);
    }

    // A started session of joined users; what the users got on their join is cleared.
    private class ObjectsWorld
    {
        public readonly TestSession Session = TestSession.Create();

        private readonly Dictionary<IUser, int> _counters = new();

        public IUser Join()
        {
            var user = Session.ReserveAndEnter();
            Session.ClearSent();
            return user;
        }

        // Created by the client of the user (EntityCreateCommand); a session entity has an ISessionEntityPayload.
        public IEntity AddEntity(IUser creator, int[] propertyIds, bool sessionEntity = false)
        {
            var counter = _counters.GetValueOrDefault(creator) + 1;
            _counters[creator] = counter;

            var id = SharedEntityIds.Create(creator.Index, counter);

            Session.Request(creator, new SharedSessionEntity.CreateRequest
            {
                Id = id,
                Properties = propertyIds.Select(propertyId => new SharedSessionObject.PropertyUpdate
                {
                    PropertyId = propertyId,
                    Value = []
                }).ToList(),
                Payload = sessionEntity == true ? new TestSessionPayload() : new TestPayload()
            });

            return Session.Entities.Entries[id];
        }

        public void SetProperty(IUser writer, int objectId, int propertyId, byte[] value)
        {
            Session.Execute(writer, new SharedSessionObject.SetProperty
            {
                ObjectId = objectId,
                PropertyId = propertyId,
                Value = value
            });
        }

        public void Destroy(IUser user, int entityId)
        {
            Session.Execute(user, new SharedSessionEntity.Destroy
            {
                EntityId = entityId
            });
        }
    }

    // The entities of a seat are forgotten with the seat: no entry is kept for a user that has left.
    [Fact]
    public void UserEntity_SeatEnds_ByUserForgetsTheSeat()
    {
        var world = new ObjectsWorld();
        var creator = world.Join();
        world.Join();
        world.AddEntity(creator, propertyIds: []);

        world.Session.Leave(creator);

        world.Session.Entities.ByUser.Should().NotContainKey(creator.Seat);
    }

    // Two keys of one id: the second service is refused and registered nowhere, a repeated request is refused again.
    [Fact]
    public void ServiceFactory_IdCollision_SecondServiceIsNotRegistered()
    {
        const string first = "Service630118";
        const string second = "Service818396";
        Service.GetId(first).Should().Be(Service.GetId(second));

        var world = new ObjectsWorld();

        world.Session.ServiceFactory.GetOrCreate(new SharedSessionService.GetRequest
        {
            Key = first,
            PropertiesIds = []
        });

        var create = () => world.Session.ServiceFactory.GetOrCreate(new SharedSessionService.GetRequest
        {
            Key = second,
            PropertiesIds = []
        });

        create.Should().Throw<InvalidOperationException>().WithMessage($"*{first}*");
        world.Session.Provider.GetRequiredService<ISessionServices>().Entries.Should().NotContainKey(second);
        create.Should().Throw<InvalidOperationException>().WithMessage($"*{first}*");
    }

    // The services of the container are set up in the session queue (they subscribe to the users, the owner and the
    // objects, which live there), not on the thread of the factory that creates the session.
    [Fact]
    public void OnSessionCreated_SetsUpTheServicesInTheQueue()
    {
        var options = new SessionCreateOptions
        {
            Type = SessionType.Match,
            ExpectedUsers = 0,
            MaxUsers = 4,
            IsPrivate = false,
            Time = new ManualTimeProvider()
        };

        var session = new TestSession(options, services => services.AddSingleton<IService>(new Service("Game")));
        var services = session.Provider.GetRequiredService<ISessionServices>();

        var created = session.Queue.SetupServices(session.ServiceFactory);

        services.Entries.Should().BeEmpty();
        created.IsCompleted.Should().BeFalse();

        session.Queue.Drain();

        created.IsCompletedSuccessfully.Should().BeTrue();
        services.Entries.Should().ContainKey("Game");
    }
}
