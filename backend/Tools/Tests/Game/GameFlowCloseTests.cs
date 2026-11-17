using Cluster.Configs;
using FluentAssertions;
using Game.GamePlay;
using Game.Session;
using Infrastructure;
using Internal;
using Meta.Matches;
using UserStatsDelta = Meta.Users.UserStatsDelta;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

// The record of the match (IMatch) is closed by the game flow when the round ends without a winner and when the
// session ends during the round; the server stream gets the start and the end of the round. The grain is a substitute
// (its close is covered by MatchRecordTests), the transactions run the action at once.
public class GameFlowCloseTests
{
    public GameFlowCloseTests()
    {
        _data = new SessionData(SessionType.Match, 2, Guid.NewGuid(), _lifetime);

        var transactions = Substitute.For<ITransactions>();
        transactions.Process(Arg.Any<TransactionParameters>()).Returns(async call => {
            await call.Arg<TransactionParameters>().Action();
            return new TransactionResult { IsSuccess = true };
        });

        var orleans = Substitute.For<IOrleans>();
        orleans.Transactions.Returns(transactions);
        orleans.Client.GetGrain<IMatch>(_data.Id, null).Returns(_match);

        _rematch.ShouldRematch(Arg.Any<IReadOnlyLifetime>(), Arg.Any<TimeSpan>()).Returns(false);

        var context = Substitute.For<IGameContext>();
        context.Boards.Returns(new Dictionary<IPlayer, IBoard>());

        AddHuman(1);
        AddHuman(2);

        _flow = new GameFlow(
            orleans,
            context,
            _users,
            Substitute.For<IPlayerFactory>(),
            _data,
            _round,
            new MatchCreateOptions { Type = GameMatchType.LastManStandingTurnBased },
            _rematch,
            Substitute.For<ISessionLogger>(),
            Substitute.For<IBotConfig>(),
            Substitute.For<ISnapshotSender>(),
            Substitute.For<IAgentObservationPublisher>(),
            new MatchStatsTracker(),
            new MatchClock(TimeProvider.System),
            _metrics);
    }

    private readonly Lifetime _lifetime = new();
    private readonly SessionUsers _users = new();
    private readonly IMatch _match = Substitute.For<IMatch>();
    private readonly IGameRound _round = Substitute.For<IGameRound>();
    private readonly IRematchAwaiter _rematch = Substitute.For<IRematchAwaiter>();
    private readonly ISessionMetrics _metrics = Substitute.For<ISessionMetrics>();
    private readonly SessionData _data;
    private readonly GameFlow _flow;

    [Fact]
    public async Task RoundWithoutWinner_MatchClosed()
    {
        _round.Process(Arg.Any<IReadOnlyLifetime>()).Returns(Guid.Empty);

        var result = await _flow.Process();

        result.Should().Be(MatchTransitionResult.End);
        await _match.Received(1).Setup(GameMatchType.LastManStandingTurnBased, Arg.Any<IReadOnlyList<Guid>>());
        await _match.Received(1).Close();
        await _match.DidNotReceive().OnComplete(Arg.Any<Guid>(), Arg.Any<Dictionary<Guid, UserStatsDelta>>());
        Received.InOrder(() => {
            _metrics.Start();
            _metrics.End(Guid.Empty);
        });
    }

    [Fact]
    public async Task SessionEndsDuringTheRound_MatchClosed()
    {
        var round = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        _round.Process(Arg.Any<IReadOnlyLifetime>()).Returns(round.Task);

        var flow = _flow.Process();
        await WaitUntil(() => _round.ReceivedCalls().Any());
        await _match.DidNotReceive().Close();

        _lifetime.Terminate();
        await WaitUntil(() => _match.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IMatch.Close)));

        // The round of a terminated session ends without a winner: the flow closes the record once more, a closed one stays.
        round.SetResult(Guid.Empty);
        await flow.WaitAsync(TimeSpan.FromSeconds(5));
        await _match.DidNotReceive().OnComplete(Arg.Any<Guid>(), Arg.Any<Dictionary<Guid, UserStatsDelta>>());
    }

    private void AddHuman(int index)
    {
        var user = Substitute.For<IUser>();
        user.Id.Returns(Guid.NewGuid());
        user.BackendId.Returns(Guid.NewGuid());
        user.Index.Returns(index);
        user.Lifetime.Returns(_lifetime.Child());
        _users.AddUser(user);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

        while (condition() == false)
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("The condition is not met in 5 s");

            await Task.Delay(10);
        }
    }
}
