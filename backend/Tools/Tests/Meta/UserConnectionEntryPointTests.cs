using System.Collections.Concurrent;
using Cluster.Configs;
using Cluster.Deploy;
using Cluster.Monitoring;
using Common.Network;
using Common.Reactive;
using FluentAssertions;
using Game.GamePlay.CardPreviews;
using Infrastructure;
using Meta.Users;
using MetaGateway.UserFlow;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shared;
using Tests.Fixtures;
using Xunit;

namespace Tests.Meta;

[GenerateSerializer]
public class TestProjectionPayload : IProjectionPayload
{
    [Id(0)] public string Name { get; set; } = string.Empty;

    public INetworkContext ToContext() => new SharedBackendUser.ProfileProjection { Name = Name };
}

/// <summary>
/// The user projection channel is subscribed before the snapshot is read; updates received before the
/// snapshot is written are buffered and sent after it. A channel gap re-reads the snapshot the same way.
/// A second connection of the same user replaces the first: closing the first keeps the second registered.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class UserConnectionEntryPointTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    private const string Previews = "previews";

    [Fact]
    public async Task Connect_SubscribesBeforeLoadingSnapshot()
    {
        var harness = new Harness(this);
        var order = new ConcurrentQueue<string>();
        harness.OnListen = () => order.Enqueue("listen");
        harness.Loader.Load(harness.UserId).Returns(_ => {
            order.Enqueue("load");
            return Snapshot("snapshot");
        });

        await harness.Connect();

        order.Should().Equal("listen", "load");
    }

    [Fact]
    public async Task UpdateDuringSnapshotLoad_SentAfterSnapshot()
    {
        var harness = new Harness(this);
        var load = new TaskCompletionSource<IReadOnlyList<IProjectionPayload>>();
        harness.Loader.Load(harness.UserId).Returns(load.Task);

        var connect = harness.Connect();
        await WaitUntil(() => harness.Listener != null);
        await harness.Listener!(Payload("update"));

        harness.Written().Should().BeEmpty();

        load.SetResult([Payload("snapshot")]);
        await connect;

        harness.Written().Should().Equal("snapshot", Previews, "update");

        await harness.Listener(Payload("live"));
        harness.Written().Should().Equal("snapshot", Previews, "update", "live");
    }

    [Fact]
    public async Task HandshakeProjections_NotSent_SnapshotReRead()
    {
        var harness = new Harness(this);
        harness.Loader.Load(harness.UserId).Returns(Snapshot("fresh"));

        await harness.Connect([Payload("stale")]);

        harness.Written().Should().Equal("fresh", Previews);
    }

    [Fact]
    public async Task GapDetected_BuffersAndResendsSnapshot()
    {
        var harness = new Harness(this);
        var reload = new TaskCompletionSource<IReadOnlyList<IProjectionPayload>>();
        harness.Loader.Load(harness.UserId).Returns(Snapshot("first"), reload.Task);

        await harness.Connect();
        harness.OnGap!();
        await harness.Listener!(Payload("replayed"));

        harness.Written().Should().Equal("first", Previews);

        reload.SetResult([Payload("second")]);
        await WaitUntil(() => harness.Written().Count == 4);

        harness.Written().Should().Equal("first", Previews, "second", "replayed");

        await harness.Listener(Payload("live"));
        harness.Written().Should().Equal("first", Previews, "second", "replayed", "live");
    }

    [Fact]
    public async Task GapResyncFails_Disconnects()
    {
        var harness = new Harness(this);
        harness.Loader.Load(harness.UserId)
               .Returns(Snapshot("first"),
                   Task.FromException<IReadOnlyList<IProjectionPayload>>(new InvalidOperationException("db down")));

        await harness.Connect();
        harness.OnGap!();

        await WaitUntil(() => harness.Lifetime.IsTerminated);
        harness.Connection.Received(1).ForceDisconnect();
    }

    [Fact]
    public async Task SnapshotLoadFails_Throws()
    {
        var harness = new Harness(this);
        harness.Loader.Load(harness.UserId).ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => harness.Connect();

        await act.Should().ThrowAsync<Exception>();
        harness.Written().Should().BeEmpty();
    }

    // Meta reconnect of the client: the new connection arrives while the old one is still open. The old one is
    // closed, and its lifetime end must not remove the new session from the connected users.
    [Fact]
    public async Task SecondConnectionOfUser_ClosesFirst_KeepsSecondConnected()
    {
        var harness = new Harness(this);
        harness.Loader.Load(harness.UserId).Returns(Snapshot("snapshot"));
        await harness.Connect();

        var second = harness.CreateSession();
        await harness.EntryPoint.OnConnected(second.Session, []);

        harness.Connection.Received(1).ForceDisconnect();
        harness.Lifetime.IsTerminated.Should().BeTrue();
        second.Session.Lifetime.IsTerminated.Should().BeFalse();

        harness.Users.IsConnected(harness.UserId).Should().BeTrue();
        harness.Users.Entries[harness.UserId].Should().BeSameAs(second.Session);

        second.Lifetime.Terminate();
        harness.Users.IsConnected(harness.UserId).Should().BeFalse();
    }

    [Fact]
    public async Task RealChannel_UpdatePublishedDuringLoad_Delivered()
    {
        var messaging = GetSiloService<IMessaging>();
        var harness = new Harness(this, messaging);
        var loadStarted = new TaskCompletionSource();
        var load = new TaskCompletionSource<IReadOnlyList<IProjectionPayload>>();

        harness.Loader.Load(harness.UserId).Returns(_ => {
            loadStarted.TrySetResult();
            return load.Task;
        });

        var connect = harness.Connect();
        await loadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await messaging.PublishChannel(new UserProjectionChannelId(harness.UserId), Payload("update"));

        load.SetResult([Payload("snapshot")]);
        await connect;

        await WaitUntil(() => harness.Written().Count == 3);
        harness.Written().Should().Equal("snapshot", Previews, "update");

        harness.Lifetime.Terminate();
    }

    private static TestProjectionPayload Payload(string name) => new() { Name = name };

    private static Task<IReadOnlyList<IProjectionPayload>> Snapshot(string name)
    {
        return Task.FromResult<IReadOnlyList<IProjectionPayload>>([Payload(name)]);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (condition() == false)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not met in 10 s");

            await Task.Delay(20);
        }
    }

    private sealed class Harness
    {
        public Harness(UserConnectionEntryPointTests test, IMessaging? messaging = null)
        {
            if (messaging == null)
            {
                messaging = Substitute.For<IMessaging>();
                var channel = Substitute.For<IRuntimeChannelClient>();
                messaging.RuntimeChannel.Returns(channel);

                channel.Listen(Arg.Any<IReadOnlyLifetime>(),
                           Arg.Any<IRuntimeChannelId>(),
                           Arg.Any<Func<IProjectionPayload, Task>>(),
                           Arg.Any<Action?>())
                       .Returns(call => {
                           OnListen?.Invoke();
                           Listener = call.ArgAt<Func<IProjectionPayload, Task>>(2);
                           OnGap = call.ArgAt<Action?>(3);
                           return Task.CompletedTask;
                       });
            }

            var previews = Substitute.For<ICardPreviewGenerator>();
            previews.GetBundlesAsync().Returns(Task.FromResult<IReadOnlyList<CardPreviewBundle>>([]));

            Users = new ConnectedUsers(Substitute.For<IDeploymentState<ConnectedUsersLiveData>>());

            EntryPoint = new UserConnectionEntryPoint(
                Users,
                Substitute.For<IUserCommandsDispatcher>(),
                messaging,
                test.GetSiloService<IOrleans>(),
                Loader,
                Substitute.For<ICardConfigs>(),
                Substitute.For<IInGameAchievementConfig>(),
                Substitute.For<IMatchMakingConfig>(),
                previews,
                NullLogger<UserConnectionEntryPoint>.Instance);

            var first = CreateSession();
            Session = first.Session;
            Connection = first.Connection;
            Lifetime = first.Lifetime;
            _written = first.Written;
        }

        private readonly ConcurrentQueue<INetworkContext> _written;

        public Guid UserId { get; } = Guid.NewGuid();
        public IUserProjectionsLoader Loader { get; } = Substitute.For<IUserProjectionsLoader>();
        public ConnectedUsers Users { get; }
        public UserConnectionEntryPoint EntryPoint { get; }
        public IUserSession Session { get; }
        public IConnection Connection { get; }
        public Lifetime Lifetime { get; }

        public Action? OnListen { get; set; }
        public Func<IProjectionPayload, Task>? Listener { get; private set; }
        public Action? OnGap { get; private set; }

        public Task Connect(IReadOnlyList<IProjectionPayload>? handshakeProjections = null)
        {
            return EntryPoint.OnConnected(Session, handshakeProjections ?? []);
        }

        // Projections and the initial card previews in the order they were written; config values are skipped.
        public IReadOnlyList<string> Written()
        {
            return _written
                   .Select(context => ((SharedBackendProjection)context).Context)
                   .Select(context => context switch
                   {
                       SharedBackendUser.ProfileProjection profile => profile.Name,
                       InitialCardPreviews => Previews,
                       _ => null
                   })
                   .OfType<string>()
                   .ToList();
        }

        // Every connection has its own lifetime; a forced disconnect ends it, as the real connection does.
        public (IUserSession Session, IConnection Connection, Lifetime Lifetime, ConcurrentQueue<INetworkContext> Written)
            CreateSession()
        {
            var written = new ConcurrentQueue<INetworkContext>();
            var writer = Substitute.For<IConnectionWriter>();

            writer.WriteOneWay(Arg.Any<INetworkContext>()).Returns(call => {
                written.Enqueue(call.Arg<INetworkContext>());
                return ValueTask.CompletedTask;
            });

            var lifetime = new Lifetime();
            var connection = Substitute.For<IConnection>();
            connection.Writer.Returns(writer);
            connection.Lifetime.Returns(lifetime);
            connection.When(c => c.ForceDisconnect()).Do(_ => lifetime.Terminate());

            var session = Substitute.For<IUserSession>();
            session.UserId.Returns(UserId);
            session.Lifetime.Returns(lifetime);
            session.Connection.Returns(connection);

            return (session, connection, lifetime, written);
        }
    }
}
