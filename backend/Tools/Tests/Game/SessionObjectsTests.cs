using Common.Network;
using Common.Reactive;
using FluentAssertions;
using Game.Session;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// N9 (port of peace-data-reborn F2.13): objects of a session. The overview of an entity carries the versions of its
/// properties; a write or destroy of an object that is already gone is dropped, of an object that never existed
/// fails explicitly (before: KeyNotFoundException from the indexer).
/// </summary>
public class SessionObjectsTests
{
    [Fact]
    public void EntityOverview_CarriesVersionOfEveryProperty()
    {
        var world = new ObjectsWorld();
        var entity = world.AddEntity(world.Join(), propertyIds: [1, 2]);

        entity.Properties[1].Update([1]);
        entity.Properties[1].Update([2]);
        entity.Properties[2].Update([3]);

        var overview = (SharedSessionEntity.CreatedOverview)entity.CreateOverview();

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
        var entity = world.AddEntity(world.Join(), propertyIds: [1]);

        var overview = (SharedSessionEntity.CreatedOverview)entity.CreateOverview();

        overview.Properties.Single().Version.Should().Be(0);
    }

    [Fact]
    public void SessionObjects_SecondObjectWithTakenId_Throws()
    {
        var objects = new SessionObjects();
        objects.Add(CreateService("Chat"));

        var add = () => objects.Add(CreateService("Chat"));

        add.Should().Throw<InvalidOperationException>().WithMessage("*service Chat*");
    }

    [Fact]
    public void SessionObjects_RemovedObject_IsRemoved_NeverAddedIsNot()
    {
        var world = new ObjectsWorld();
        var entity = world.AddEntity(world.Join(), propertyIds: [1]);

        world.Objects.IsRemoved(entity.Id).Should().BeFalse();

        entity.Destroy();

        world.Objects.Entries.Should().NotContainKey(entity.Id);
        world.Objects.IsRemoved(entity.Id).Should().BeTrue();
        world.Objects.IsRemoved(entity.Id + 1).Should().BeFalse();
    }

    [Fact]
    public void SetProperty_LiveEntity_Applies_SendsToOthers()
    {
        var world = new ObjectsWorld();
        var writer = world.Join();
        var other = world.Join();
        var entity = world.AddEntity(writer, propertyIds: [1]);

        world.SetProperty(writer, entity.Id, 1, [7]);

        entity.Properties[1].RawValue.Should().Equal(7);
        world.SentTo(other).OfType<SharedSessionObject.PropertyUpdate>().Should().ContainSingle(update =>
            update.ObjectId == entity.Id && update.PropertyId == 1 && update.Version == 1);
        world.SentTo(writer).Should().BeEmpty();
    }

    [Fact]
    public void SetProperty_DestroyedEntity_IsDropped()
    {
        var world = new ObjectsWorld();
        var writer = world.Join();
        var other = world.Join();
        var entity = world.AddEntity(other, propertyIds: [1]);
        entity.Destroy();

        var write = () => world.SetProperty(writer, entity.Id, 1, [7]);

        write.Should().NotThrow();
        world.SentTo(other).Should().BeEmpty();
    }

    [Fact]
    public void SetProperty_EntityOfUserThatLeft_IsDropped()
    {
        var world = new ObjectsWorld();
        var writer = world.Join();
        var owner = world.Join();
        var entity = world.AddEntity(owner, propertyIds: [1]);

        // The owner left: its entities end with it, a write from the other user crossed that.
        owner.Lifetime.Terminate();

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

        world.Destroy(owner, entity.Id);

        entity.Lifetime.IsTerminated.Should().BeTrue();
        world.Entities.Entries.Should().NotContainKey(entity.Id);
        world.SentTo(other).OfType<SharedSessionEntity.DestroyUpdate>().Should()
             .ContainSingle(update => update.EntityId == entity.Id);
        world.SentTo(owner).Should().BeEmpty();
    }

    [Fact]
    public void Destroy_AlreadyDestroyedEntity_IsDropped()
    {
        var world = new ObjectsWorld();
        var owner = world.Join();
        var other = world.Join();
        var entity = world.AddEntity(owner, propertyIds: [1]);
        entity.Destroy();

        var destroy = () => world.Destroy(owner, entity.Id);

        destroy.Should().NotThrow();
        world.SentTo(other).Should().BeEmpty();
    }

    [Fact]
    public void Destroy_UnknownEntity_Throws()
    {
        var world = new ObjectsWorld();
        var owner = world.Join();

        var destroy = () => world.Destroy(owner, 4242);

        destroy.Should().Throw<InvalidOperationException>().WithMessage("*entity 4242*does not exist*");
    }

    // As ServiceFactory: set up on the session lifetime before it is added.
    private static Service CreateService(string key)
    {
        var service = new Service(key);
        service.Setup(new Lifetime(), Substitute.For<IPropertyUpdateSender>());
        return service;
    }

    private class ObjectsWorld
    {
        public ObjectsWorld()
        {
            _factory = new EntityFactory(Users, Entities, Objects, new PropertyUpdateSender(Users));
        }

        public readonly SessionUsers Users = new();
        public readonly SessionEntities Entities = new();
        public readonly SessionObjects Objects = new();

        private readonly EntityFactory _factory;
        private readonly Dictionary<IUser, List<INetworkContext>> _sent = new();

        private int _entityId = 100;

        public IUser Join()
        {
            var user = Substitute.For<IUser>();
            var connection = Substitute.For<IConnection>();
            var writer = Substitute.For<IConnectionWriter>();
            var sent = new List<INetworkContext>();

            writer.When(target => target.WriteOneWay(Arg.Any<INetworkContext>()))
                  .Do(call => sent.Add(call.Arg<INetworkContext>()));

            connection.Writer.Returns(writer);
            user.Connection.Returns(connection);
            user.Index.Returns(Users.GetNextIndex());
            user.Lifetime.Returns(new Lifetime());

            _sent[user] = sent;
            Users.AddUser(user);
            return user;
        }

        public IReadOnlyList<INetworkContext> SentTo(IUser user)
        {
            return _sent[user];
        }

        public IEntity AddEntity(IUser owner, int[] propertyIds)
        {
            _entityId++;

            return _factory.Create(new SharedSessionEntity.CreateRequest
            {
                Id = _entityId,
                Properties = propertyIds.Select(id => new SharedSessionObject.PropertyUpdate
                {
                    PropertyId = id,
                    Value = []
                }).ToList(),
                Payload = Substitute.For<IEntityPayload>()
            }, owner);
        }

        public void SetProperty(IUser writer, int objectId, int propertyId, byte[] value)
        {
            ICommand command = new SetPropertyCommand(Objects, Users, NullLogger<SetPropertyCommand>.Instance);

            command.Execute(writer, new SharedSessionObject.SetProperty
            {
                ObjectId = objectId,
                PropertyId = propertyId,
                Value = value
            });
        }

        public void Destroy(IUser user, int entityId)
        {
            ICommand command = new EntityDestroyCommand(Entities, Objects, Users, NullLogger<EntityDestroyCommand>.Instance);

            command.Execute(user, new SharedSessionEntity.Destroy
            {
                EntityId = entityId
            });
        }
    }
}
