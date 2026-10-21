using System.Collections.Concurrent;
using Cluster.Configs;
using Cluster.Deploy;
using Cluster.Monitoring;
using Cluster.State;
using Common;
using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Infrastructure.Startup;
using Meta.Matches;
using Meta.Users;
using MetaGateway.Matchmaking;
using MetaGateway.UserFlow;
using MetaGateway.UserFlow.Connection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Meta;

public class MatchmakingTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly IMatchFactory _matchFactory;
    private readonly ILobbyFactory _lobbyFactory;
    private readonly IConnectedUsers _users;
    private readonly IBotConfig _botConfig;
    private readonly IGameFlags _clusterFlags;
    private readonly IClusterParticipantContext _participantContext;
    private readonly IMessaging _messaging;
    private readonly ILogger<Matchmaking> _logger;
    private readonly ViewableProperty<bool> _isInitialized = new(false);
    private readonly ConcurrentQueue<(string Channel, MatchSearchResultUpdate Update)> _sent = new();
    private readonly Matchmaking _sut;

    public MatchmakingTests()
    {
        _matchFactory = Substitute.For<IMatchFactory>();
        _lobbyFactory = Substitute.For<ILobbyFactory>();
        _users = Substitute.For<IConnectedUsers>();
        _botConfig = Substitute.For<IBotConfig>();
        _clusterFlags = Substitute.For<IGameFlags>();
        _participantContext = Substitute.For<IClusterParticipantContext>();
        _messaging = Substitute.For<IMessaging>();
        _logger = Substitute.For<ILogger<Matchmaking>>();

        _botConfig.Value.Returns(new BotConfigOptions());
        _clusterFlags.MatchmakingEnabled.Returns(true);

        _participantContext.IsInitialized.Returns(_isInitialized);

        var channel = Substitute.For<IRuntimeChannelClient>();
        channel.Publish(Arg.Any<IRuntimeChannelId>(), Arg.Any<object>()).Returns(call => {
            _sent.Enqueue((call.Arg<IRuntimeChannelId>().ToRaw(), (MatchSearchResultUpdate)call.Arg<object>()));
            return Task.CompletedTask;
        });
        _messaging.RuntimeChannel.Returns(channel);

        var liveData = Substitute.For<IDeploymentState<MatchmakingLiveData>>();

        _sut = new Matchmaking(_matchFactory,
            _lobbyFactory,
            _users,
            _botConfig,
            _clusterFlags,
            _participantContext,
            liveData,
            _messaging,
            _logger);
    }

    [Fact]
    public async Task CancelMatchSearch_NonExistentUser_DoesNotThrow()
    {
        var act = () => _sut.CancelMatchSearch(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    // N10: the command answers with the result itself, no projection is sent.
    [Fact]
    public async Task Create_ReturnsResultOfTheUser()
    {
        var userId = Guid.NewGuid();
        var match = CreatedMatch(GameMatchType.Single, userId);

        _matchFactory.Create(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<GameMatchType>()).Returns(match);

        var result = await _sut.Create(userId, GameMatchType.Single);

        await _matchFactory.Received(1)
                           .Create(Arg.Is<IReadOnlyList<Guid>>(list => list.Count == 1 && list[0] == userId),
                               GameMatchType.Single);
        await _matchFactory.DidNotReceive().CreateWithBot(Arg.Any<Guid>(), Arg.Any<GameMatchType>());
        ShouldBeMatched(result, match, userId, Guid.Empty);
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateWithBot_ReturnsResultOfTheUser()
    {
        var userId = Guid.NewGuid();
        var match = CreatedMatch(GameMatchType.TimeLimited, userId);

        _matchFactory.CreateWithBot(Arg.Any<Guid>(), Arg.Any<GameMatchType>()).Returns(match);

        var result = await _sut.CreateWithBot(userId, GameMatchType.TimeLimited);

        await _matchFactory.Received(1).CreateWithBot(userId, GameMatchType.TimeLimited);
        await _matchFactory.DidNotReceive().Create(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<GameMatchType>());
        ShouldBeMatched(result, match, userId, Guid.Empty);
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchLobby_ReturnsLobbyFactoryResult()
    {
        var userId = Guid.NewGuid();
        var lobby = new SharedMatchmaking.LobbyResult { SessionId = Guid.NewGuid(), ServerUrl = "ws://x", Ticket = "t" };
        _lobbyFactory.GetOrCreate(Arg.Any<Guid>()).Returns(lobby);

        var result = await _sut.SearchLobby(userId);

        await _lobbyFactory.Received(1).GetOrCreate(userId);
        result.Should().BeSameAs(lobby);
    }

    [Fact]
    public async Task SearchMatch_ThreadSafe_ConcurrentOperations()
    {
        var userIds = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();
        var tasks = userIds.Select(id => _sut.SearchMatch(id, GameMatchType.Single, Guid.NewGuid())).ToList();

        await Task.WhenAll(tasks);

        // Verify all users can be cancelled (proves they were all added)
        foreach (var userId in userIds)
            await _sut.CancelMatchSearch(userId);

        // Verify no spontaneous match creation happened during concurrent adds
        await _matchFactory.DidNotReceive().Create(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<GameMatchType>());
    }

    [Fact]
    public async Task CancelMatchSearch_ThreadSafe_ConcurrentCancellations()
    {
        var userIds = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToList();

        // Add all users
        foreach (var userId in userIds)
            await _sut.SearchMatch(userId, GameMatchType.Single, Guid.NewGuid());

        // Cancel all concurrently
        var tasks = userIds.Select(id => _sut.CancelMatchSearch(id)).ToList();
        await Task.WhenAll(tasks);

        // Verify cleanup: re-adding same users should succeed without errors
        foreach (var userId in userIds)
            await _sut.SearchMatch(userId, GameMatchType.Single, Guid.NewGuid());

        // Verify no spontaneous match creation happened
        await _matchFactory.DidNotReceive().Create(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<GameMatchType>());
    }

    // Meta reconnect: the client opens a new meta connection, the old one closes after it. The user searching
    // a match must stay in the queue and get matched, not be dropped as "not connected".
    [Fact]
    public async Task Loop_UserReconnectedDuringSearch_StaysQueuedAndMatched()
    {
        var users = new ConnectedUsers(Substitute.For<IServiceSlicedState<ConnectedUsersLiveData, List<ConnectedUserEntry>>>());
        var sut = new Matchmaking(_matchFactory,
            _lobbyFactory,
            users,
            _botConfig,
            _clusterFlags,
            _participantContext,
            Substitute.For<IDeploymentState<MatchmakingLiveData>>(),
            _messaging,
            _logger);

        var reconnecting = Guid.NewGuid();
        var other = Guid.NewGuid();
        var oldLifetime = new Lifetime();
        var created = new TaskCompletionSource<IReadOnlyList<Guid>>();

        _matchFactory.Create(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<GameMatchType>()).Returns(call => {
            var participants = call.Arg<IReadOnlyList<Guid>>();
            created.TrySetResult(participants);
            return CreatedMatch(GameMatchType.Single, participants.ToArray());
        });

        users.Add(UserSession(reconnecting, oldLifetime));
        users.Add(UserSession(other, new Lifetime()));
        await sut.SearchMatch(reconnecting, GameMatchType.Single, Guid.NewGuid());

        // UserConnectionEntryPoint order: the new session replaces the old one, then the old one is closed.
        users.Add(UserSession(reconnecting, new Lifetime()));
        oldLifetime.Terminate();

        await sut.SearchMatch(other, GameMatchType.Single, Guid.NewGuid());

        var lifetime = new Lifetime();
        _isInitialized.Set(true);
        await sut.OnCoordinatorSetupCompleted(lifetime);

        try
        {
            var matched = await created.Task.WaitAsync(Wait, TestContext.Current.CancellationToken);
            matched.Should().BeEquivalentTo([reconnecting, other]);
        }
        finally
        {
            lifetime.Terminate();
        }
    }

    // N10: each player of the pair gets the result of its own search (its RequestId) to its own projection channel.
    [Fact]
    public async Task Loop_PairMatched_EachGetsResultWithOwnRequestId()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var firstRequest = Guid.NewGuid();
        var secondRequest = Guid.NewGuid();
        var match = CreatedMatch(GameMatchType.Single, first, second);

        _users.IsConnected(Arg.Any<Guid>()).Returns(true);
        _matchFactory.Create(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<GameMatchType>()).Returns(match);

        await _sut.SearchMatch(first, GameMatchType.Single, firstRequest);
        await _sut.SearchMatch(second, GameMatchType.Single, secondRequest);

        await RunLoopUntil(() => _sent.Count >= 2);

        _sent.Should().HaveCount(2);
        ShouldBeMatched(Sent(first).ToContext(), match, first, firstRequest);
        ShouldBeMatched(Sent(second).ToContext(), match, second, secondRequest);
    }

    // A repeated search replaces the queued one: the result carries the id of the latest search.
    [Fact]
    public async Task Loop_SearchRepeated_ResultCarriesLatestRequestId()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var latest = Guid.NewGuid();

        _users.IsConnected(Arg.Any<Guid>()).Returns(true);
        _matchFactory.Create(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<GameMatchType>())
                     .Returns(CreatedMatch(GameMatchType.Single, first, second));

        await _sut.SearchMatch(first, GameMatchType.Single, Guid.NewGuid());
        await _sut.SearchMatch(first, GameMatchType.Single, latest);
        await _sut.SearchMatch(second, GameMatchType.Single, Guid.NewGuid());

        await RunLoopUntil(() => _sent.Count >= 2);

        Sent(first).RequestId.Should().Be(latest);
    }

    // B1/N10: a failed creation ends the search of both players with Failed (their own RequestId) and is not
    // retried: a lasting failure must not spin the loop.
    [Fact]
    public async Task Loop_MatchCreationFails_BothGetFailedAndNoRetry()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var firstRequest = Guid.NewGuid();
        var secondRequest = Guid.NewGuid();

        _users.IsConnected(Arg.Any<Guid>()).Returns(true);
        _matchFactory.Create(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<GameMatchType>())
                     .Returns(Task.FromException<CreatedMatch>(new InvalidOperationException("setup failed")));

        await _sut.SearchMatch(first, GameMatchType.Single, firstRequest);
        await _sut.SearchMatch(second, GameMatchType.Single, secondRequest);

        // The loop keeps running for a while after the failure: a requeue would create the match again.
        await RunLoopUntil(() => _sent.Count >= 2, TimeSpan.FromMilliseconds(500));

        await _matchFactory.Received(1).Create(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<GameMatchType>());
        _sent.Should().HaveCount(2);
        ShouldBeFailed(Sent(first), GameMatchType.Single, firstRequest);
        ShouldBeFailed(Sent(second), GameMatchType.Single, secondRequest);

        _logger.Received().Log(LogLevel.Error, Arg.Any<EventId>(), Arg.Any<object>(),
            Arg.Is<Exception>(e => e.Message == "setup failed"), Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Loop_BotMatchCreationFails_PlayerGetsFailed()
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        _botConfig.Value.Returns(new BotConfigOptions { MatchmakingApplyThreshold = 0 });
        _users.IsConnected(Arg.Any<Guid>()).Returns(true);
        _matchFactory.CreateWithBot(Arg.Any<Guid>(), Arg.Any<GameMatchType>())
                     .Returns(Task.FromException<CreatedMatch>(new InvalidOperationException("no game server")));

        await _sut.SearchMatch(userId, GameMatchType.TimeLimited, requestId);

        await RunLoopUntil(() => _sent.Count >= 1, TimeSpan.FromMilliseconds(500));

        await _matchFactory.Received(1).CreateWithBot(userId, GameMatchType.TimeLimited);
        _sent.Should().ContainSingle();
        ShouldBeFailed(Sent(userId), GameMatchType.TimeLimited, requestId);
    }

    // The result of one player is not lost when the delivery to the other one fails.
    [Fact]
    public async Task Loop_DeliveryToOneFails_OtherStillGetsResult()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var secondRequest = Guid.NewGuid();
        var match = CreatedMatch(GameMatchType.Single, first, second);
        var firstChannel = new UserProjectionChannelId(first).ToRaw();

        _users.IsConnected(Arg.Any<Guid>()).Returns(true);
        _matchFactory.Create(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<GameMatchType>()).Returns(match);

        _messaging.RuntimeChannel.Publish(Arg.Any<IRuntimeChannelId>(), Arg.Any<object>()).Returns(call => {
            var channel = call.Arg<IRuntimeChannelId>().ToRaw();

            if (channel == firstChannel)
                return Task.FromException(new InvalidOperationException("channel is gone"));

            _sent.Enqueue((channel, (MatchSearchResultUpdate)call.Arg<object>()));
            return Task.CompletedTask;
        });

        await _sut.SearchMatch(first, GameMatchType.Single, Guid.NewGuid());
        await _sut.SearchMatch(second, GameMatchType.Single, secondRequest);

        await RunLoopUntil(() => _sent.Count >= 1);

        ShouldBeMatched(Sent(second).ToContext(), match, second, secondRequest);

        _logger.Received().Log(LogLevel.Error, Arg.Any<EventId>(), Arg.Any<object>(),
            Arg.Is<Exception>(e => e.Message == "channel is gone"), Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SearchMatchCommand_WithoutRequestId_Throws()
    {
        var command = new MatchmakingCommands.SearchMatch(_sut);
        var session = UserSession(Guid.NewGuid(), new Lifetime());

        var act = () => ((MetaGateway.UserFlow.Commands.IUserCommand)command).Execute(session,
            new SharedMatchmaking.SearchMatch { Type = GameMatchType.Single });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private async Task RunLoopUntil(Func<bool> condition, TimeSpan? settle = null)
    {
        var lifetime = new Lifetime();
        _isInitialized.Set(true);
        await _sut.OnCoordinatorSetupCompleted(lifetime);

        try
        {
            var deadline = DateTime.UtcNow + Wait;

            while (condition() == false)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("The matchmaking loop did not reach the expected state");

                await Task.Delay(10, TestContext.Current.CancellationToken);
            }

            if (settle != null)
                await Task.Delay(settle.Value, TestContext.Current.CancellationToken);
        }
        finally
        {
            lifetime.Terminate();
        }
    }

    private MatchSearchResultUpdate Sent(Guid userId)
    {
        var channel = new UserProjectionChannelId(userId).ToRaw();
        return _sent.Single(entry => entry.Channel == channel).Update;
    }

    private static CreatedMatch CreatedMatch(GameMatchType type, params Guid[] participants)
    {
        return new CreatedMatch
        {
            SessionId = Guid.NewGuid(),
            ServerUrl = "ws://game",
            Type = type,
            Tickets = participants.ToDictionary(id => id, id => $"ticket-{id}")
        };
    }

    private static void ShouldBeMatched(INetworkContext context, CreatedMatch match, Guid userId, Guid requestId)
    {
        var result = context.Should().BeOfType<SharedMatchmaking.MatchResult>().Subject;
        result.Status.Should().Be(SharedMatchmaking.MatchSearchStatus.Matched);
        result.RequestId.Should().Be(requestId);
        result.SessionId.Should().Be(match.SessionId);
        result.ServerUrl.Should().Be(match.ServerUrl);
        result.Type.Should().Be(match.Type);
        result.Ticket.Should().Be($"ticket-{userId}");
    }

    private static void ShouldBeFailed(MatchSearchResultUpdate update, GameMatchType type, Guid requestId)
    {
        var result = update.ToContext().Should().BeOfType<SharedMatchmaking.MatchResult>().Subject;
        result.Status.Should().Be(SharedMatchmaking.MatchSearchStatus.Failed);
        result.RequestId.Should().Be(requestId);
        result.Type.Should().Be(type);
        result.Ticket.Should().BeNull();
        result.SessionId.Should().Be(Guid.Empty);
    }

    private static IUserSession UserSession(Guid userId, Lifetime lifetime)
    {
        var session = Substitute.For<IUserSession>();
        session.UserId.Returns(userId);
        session.Lifetime.Returns(lifetime);
        return session;
    }
}
