using FluentAssertions;
using Game.GamePlay;
using Game.Session;
using UserStatsDelta = Meta.Users.UserStatsDelta;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

// The clock of the match (IMatchClock) and its stamp on every snapshot (SnapshotSender, SharedMoveSnapshot.MatchMs).
public class MatchClockTests
{
    private readonly SteppedTimeProvider _time = new();
    private readonly List<SharedMoveSnapshot> _sent = new();

    [Fact]
    public void BeforeStart_Zero()
    {
        var clock = new MatchClock(_time);
        _time.Advance(TimeSpan.FromSeconds(5));

        clock.Ms.Should().Be(0);
        clock.Seconds.Should().Be(0);
    }

    [Fact]
    public void Started_CountsFromTheStart()
    {
        var clock = new MatchClock(_time);
        _time.Advance(TimeSpan.FromSeconds(5));
        clock.Start();

        _time.Advance(TimeSpan.FromMilliseconds(1250));

        clock.Ms.Should().Be(1250);
        clock.Seconds.Should().Be(1.25);
    }

    [Fact]
    public void StartTwice_Throws()
    {
        var clock = new MatchClock(_time);
        clock.Start();

        var start = () => clock.Start();

        start.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Send_StampsEverySnapshot_TheStampGrows()
    {
        var (sender, clock) = CreateSender();
        clock.Start();

        _time.Advance(TimeSpan.FromMilliseconds(1500));
        sender.Send(new MoveSnapshot());
        _time.Advance(TimeSpan.FromSeconds(1));
        sender.Send(new MoveSnapshot());

        _sent.Select(snapshot => snapshot.MatchMs).Should().Equal(1500, 2500);
    }

    [Fact]
    public void Send_TheLastSnapshotOfTheGame_Stamped()
    {
        var (sender, clock) = CreateSender();
        clock.Start();
        _time.Advance(TimeSpan.FromSeconds(42));

        var completion = new MoveSnapshot();
        completion.RecordGameCompleted(Guid.Empty, [], new Dictionary<Guid, UserStatsDelta>(), null, []);
        sender.Send(completion);

        _sent.Single().MatchMs.Should().Be(42000);
        _sent.Single().Records.Single().Should().BeOfType<GameCompletedRecord>();
    }

    private (SnapshotSender Sender, MatchClock Clock) CreateSender()
    {
        var connection = Substitute.For<IUserConnection>();
        connection.When(c => c.Send(Arg.Any<INetworkContext>()))
                  .Do(call => _sent.Add((SharedMoveSnapshot)call.Arg<INetworkContext>()));

        var user = Substitute.For<IUser>();
        user.Connection.Returns(connection);

        var context = Substitute.For<IGameContext>();
        context.UserToPlayer.Returns(new Dictionary<IUser, IPlayer> { { user, Substitute.For<IPlayer>() } });

        var clock = new MatchClock(_time);
        return (new SnapshotSender(context, clock), clock);
    }
}
