using FluentAssertions;
using Game.GamePlay;
using Game.MatchMetrics;
using Game.Session;
using Internal;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

// The stream of the server of a match (SessionMetrics): the connected humans, their joins and leaves while the session
// runs, the end of the round with the index of the winner. The users are the real list of a session, the writer records.
public class SessionMetricsTests
{
    public SessionMetricsTests()
    {
        _data = new SessionData(SessionType.Match, 2, Guid.NewGuid(), _lifetime);
        _clock = new MatchClock(_time);
        _clock.Start();

        // The queue of the session runs the action at once: the tests are the only thread.
        var queue = Substitute.For<IExecutionQueue>();
        queue.When(q => q.Enqueue(Arg.Any<Action>())).Do(call => call.Arg<Action>()());

        _metrics = new SessionMetrics(_writer, _data, _users, queue, _clock);
    }

    private readonly Lifetime _lifetime = new();
    private readonly SessionUsers _users = new();
    private readonly SteppedTimeProvider _time = new();
    private readonly RecordingMetricsWriter _writer = new();
    private readonly SessionData _data;
    private readonly MatchClock _clock;
    private readonly SessionMetrics _metrics;

    [Fact]
    public void Start_HumansJoin_BotNotCounted()
    {
        AddUser(1);
        AddUser(2, isBot: true);

        _metrics.Start();

        _writer.MatchId.Should().Be(_data.Id);
        _writer.Events.Select(entry => (entry.Name, entry.Number("index"))).Should().Equal((SessionMetrics.JoinEvent, 1));
        _writer.Gauges.Should().Equal(0, 1);
    }

    [Fact]
    public void HumanJoinsLater_Counted()
    {
        AddUser(1);
        _metrics.Start();

        AddUser(2);

        _writer.Events.Select(entry => entry.Number("index")).Should().Equal(1, 2);
        _writer.Gauges.Should().Equal(0, 1, 2);
    }

    [Fact]
    public void HumanLeaves_IndexAndSecondsOfTheMatch()
    {
        var first = AddUser(1);
        AddUser(2);
        _metrics.Start();

        _time.Advance(TimeSpan.FromSeconds(12.34));
        first.Terminate();

        var leave = _writer.Events.Last();
        leave.Name.Should().Be(SessionMetrics.LeaveEvent);
        leave.Number("index").Should().Be(1);
        leave.Number("seconds").Should().Be(12.3);
        _writer.Gauges.Should().Equal(0, 1, 2, 1);
    }

    [Fact]
    public void SessionEnds_NoLeaves()
    {
        AddUser(1);
        AddUser(2);
        _metrics.Start();

        _lifetime.Terminate();

        _writer.Events.Select(entry => entry.Name).Should().OnlyContain(name => name == SessionMetrics.JoinEvent);
        _writer.Gauges.Should().Equal(0, 1, 2);
    }

    [Fact]
    public void End_WithWinner_IndexOfTheWinner()
    {
        AddUser(1);
        var bot = AddUser(2, isBot: true);
        _metrics.Start();
        _time.Advance(TimeSpan.FromSeconds(95));

        _metrics.End(bot.Id);

        var end = _writer.Events.Last();
        end.Name.Should().Be(SessionMetrics.EndEvent);
        end.Number("seconds").Should().Be(95);
        end.Number("winner_index").Should().Be(2);
    }

    [Fact]
    public void End_WinnerLeftBefore_IndexOfTheStart()
    {
        var first = AddUser(1);
        AddUser(2);
        _metrics.Start();
        var winnerId = first.Id;
        first.Terminate();

        _metrics.End(winnerId);

        _writer.Events.Last().Number("winner_index").Should().Be(1);
    }

    [Fact]
    public void End_NoWinner_MinusOne()
    {
        AddUser(1);
        _metrics.Start();

        _metrics.End(Guid.Empty);

        _writer.Events.Last().Number("winner_index").Should().Be(SessionMetrics.NoWinner);
    }

    [Fact]
    public void End_AfterTheSession_NothingWritten()
    {
        AddUser(1);
        _metrics.Start();
        var count = _writer.Events.Count;
        _lifetime.Terminate();

        _metrics.End(Guid.Empty);

        _writer.Events.Should().HaveCount(count);
    }

    [Fact]
    public void End_UnknownWinner_Throws()
    {
        AddUser(1);
        _metrics.Start();

        var end = () => _metrics.End(Guid.NewGuid());

        end.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void StartTwice_Throws()
    {
        _metrics.Start();

        var start = () => _metrics.Start();

        start.Should().Throw<InvalidOperationException>();
    }

    private TestUser AddUser(int index, bool isBot = false)
    {
        var lifetime = _lifetime.Child();
        var user = Substitute.For<IUser>();
        var id = Guid.NewGuid();
        user.Id.Returns(id);
        user.Index.Returns(index);
        user.IsBot.Returns(isBot);
        user.Lifetime.Returns(lifetime);
        _users.AddUser(user);

        return new TestUser(id, lifetime);
    }

    private record TestUser(Guid Id, ILifetime Lifetime)
    {
        public void Terminate()
        {
            Lifetime.Terminate();
        }
    }
}
