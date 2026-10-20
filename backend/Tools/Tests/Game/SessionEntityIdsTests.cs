using Common.Network;
using Common.Reactive;
using FluentAssertions;
using Game.Session;
using NSubstitute;
using Shared;
using Xunit;
using ErrorCode = Shared.ErrorCode;

namespace Tests.Game;

/// <summary>
/// N4 (port of the CreateRequest check of peace-data-reborn F2.19): a client creates an entity only in its own id
/// range (SessionEntityIds) and never with a used id. Before: any id was accepted, and a client could take the id of
/// another user's player before the session built it - the build then failed and the match never started.
/// </summary>
public class SessionEntityIdsTests
{
    [Fact]
    public void Create_InOwnRange_SendsOverviewToOthers()
    {
        var world = new IdsWorld();
        var creator = world.Join();
        var other = world.Join();
        var id = creator.Index * SessionEntityIds.Stride + 50;

        var response = world.Create(creator, id);

        response.EntityId.Should().Be(id);
        world.SentTo(other).OfType<SharedSessionEntity.CreatedOverview>().Single().EntityId.Should().Be(id);
        world.SentTo(creator).Should().BeEmpty();
    }

    [Fact]
    public void Create_PlayerIdOfAnotherUser_IsForbidden_SessionStillBuildsThePlayer()
    {
        var world = new IdsWorld();
        var first = world.Join();
        var second = world.Join();

        // The first id of a user's range is its player, built by the session (GameFlow -> PlayerFactory).
        var playerId = second.Index * SessionEntityIds.Stride + 1;

        var create = () => world.Create(first, playerId);

        create.Should().Throw<SessionRequestException>()
              .Where(e => e.Code == ErrorCode.Forbidden)
              .WithMessage("*out of its id range*");

        world.Objects.Entries.Should().BeEmpty();
        world.SentTo(second).Should().BeEmpty();

        var player = world.Build(second);
        player.Id.Should().Be(playerId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100_000)]
    [InlineData(200_000)]
    public void Create_InvalidId_IsForbidden(int id)
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
        var id = user.Index * SessionEntityIds.Stride + 50;
        world.Create(user, id);

        var again = () => world.Create(user, id);

        again.Should().Throw<SessionRequestException>().WithMessage("*already used*");
    }

    [Fact]
    public void Create_DestroyedId_IsForbidden()
    {
        var world = new IdsWorld();
        var user = world.Join();
        var id = user.Index * SessionEntityIds.Stride + 50;
        world.Create(user, id);
        world.Entities.Entries[id].Destroy();

        var again = () => world.Create(user, id);

        again.Should().Throw<SessionRequestException>().WithMessage("*already used*");
    }

    private class IdsWorld
    {
        public IdsWorld()
        {
            _factory = new EntityFactory(Users, Entities, Objects, new PropertyUpdateSender(Users));
            _create = new EntityCreateCommand(_factory, Objects, Users);
        }

        public readonly SessionUsers Users = new();
        public readonly SessionEntities Entities = new();
        public readonly SessionObjects Objects = new();

        private readonly EntityFactory _factory;
        private readonly IResponseCommand _create;
        private readonly Dictionary<IUser, List<INetworkContext>> _sent = new();

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

        public SharedSessionEntity.CreateResponse Create(IUser user, int id)
        {
            var request = new SharedSessionEntity.CreateRequest
            {
                Id = id,
                Properties = [],
                Payload = Substitute.For<IEntityPayload>()
            };

            return (SharedSessionEntity.CreateResponse)_create.Execute(user, request);
        }

        // An entity the session builds for the user, as PlayerFactory does.
        public IEntity Build(IUser owner)
        {
            return _factory.Create(owner)
                           .WithPayload(Substitute.For<IEntityPayload>())
                           .Build();
        }
    }
}
