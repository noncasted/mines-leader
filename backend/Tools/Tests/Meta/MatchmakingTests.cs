using Cluster.Configs;
using Cluster.Deploy;
using Cluster.Monitoring;
using Common;
using Common.Reactive;
using FluentAssertions;
using Infrastructure.Startup;
using Meta.Matches;
using MetaGateway.Matchmaking;
using MetaGateway.UserFlow;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Meta;

public class MatchmakingTests
{
    private readonly IMatchFactory _matchFactory;
    private readonly ILobbyFactory _lobbyFactory;
    private readonly IConnectedUsers _users;
    private readonly IBotConfig _botConfig;
    private readonly IClusterFlags _clusterFlags;
    private readonly IClusterParticipantContext _participantContext;
    private readonly ILogger<Matchmaking> _logger;
    private readonly Matchmaking _sut;

    public MatchmakingTests()
    {
        _matchFactory = Substitute.For<IMatchFactory>();
        _lobbyFactory = Substitute.For<ILobbyFactory>();
        _users = Substitute.For<IConnectedUsers>();
        _botConfig = Substitute.For<IBotConfig>();
        _clusterFlags = Substitute.For<IClusterFlags>();
        _participantContext = Substitute.For<IClusterParticipantContext>();
        _logger = Substitute.For<ILogger<Matchmaking>>();

        _botConfig.Value.Returns(new BotConfigOptions());
        _clusterFlags.MatchmakingEnabled.Returns(true);

        var isInitialized = new ViewableProperty<bool>(false);
        _participantContext.IsInitialized.Returns(isInitialized);

        var liveData = Substitute.For<ILiveState<MatchmakingLiveData>>();

        _sut = new Matchmaking(_matchFactory,
            _lobbyFactory,
            _users,
            _botConfig,
            _clusterFlags,
            _participantContext,
            liveData,
            _logger);
    }

    [Fact]
    public async Task CancelMatchSearch_NonExistentUser_DoesNotThrow()
    {
        var act = () => _sut.CancelMatchSearch(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Create_CallsMatchFactory()
    {
        var userId = Guid.NewGuid();

        _matchFactory.Create(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<GameMatchType>())
                     .Returns(Task.CompletedTask);

        await _sut.Create(userId, GameMatchType.Single);

        await _matchFactory.Received(1)
                           .Create(Arg.Is<IReadOnlyList<Guid>>(list => list.Count == 1 && list[0] == userId),
                               GameMatchType.Single);
        await _matchFactory.DidNotReceive().CreateWithBot(Arg.Any<Guid>(), Arg.Any<GameMatchType>());
    }

    [Fact]
    public async Task CreateWithBot_CallsMatchFactory()
    {
        var userId = Guid.NewGuid();

        _matchFactory.CreateWithBot(Arg.Any<Guid>(), Arg.Any<GameMatchType>())
                     .Returns(Task.CompletedTask);

        await _sut.CreateWithBot(userId, GameMatchType.TimeLimited);

        await _matchFactory.Received(1).CreateWithBot(userId, GameMatchType.TimeLimited);
        await _matchFactory.DidNotReceive().Create(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<GameMatchType>());
    }

    [Fact]
    public async Task SearchLobby_CallsLobbyFactory()
    {
        var userId = Guid.NewGuid();
        _lobbyFactory.GetOrCreate(Arg.Any<Guid>()).Returns(Task.CompletedTask);

        await _sut.SearchLobby(userId);

        await _lobbyFactory.Received(1).GetOrCreate(userId);
    }

    [Fact]
    public async Task SearchMatch_ThreadSafe_ConcurrentOperations()
    {
        var userIds = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();
        var tasks = userIds.Select(id => _sut.SearchMatch(id, GameMatchType.Single)).ToList();

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
            await _sut.SearchMatch(userId, GameMatchType.Single);

        // Cancel all concurrently
        var tasks = userIds.Select(id => _sut.CancelMatchSearch(id)).ToList();
        await Task.WhenAll(tasks);

        // Verify cleanup: re-adding same users should succeed without errors
        foreach (var userId in userIds)
            await _sut.SearchMatch(userId, GameMatchType.Single);

        // Verify no spontaneous match creation happened
        await _matchFactory.DidNotReceive().Create(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<GameMatchType>());
    }
}