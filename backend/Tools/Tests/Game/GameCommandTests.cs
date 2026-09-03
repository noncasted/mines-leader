using FluentAssertions;
using Game.GamePlay;
using Game.GamePlay.Snapshots;
using Game.Session;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Исключение внутри команды должно уходить агенту как кадр с HasError, иначе мост ждёт
/// наблюдение 10 секунд и отвечает таймаутом.
/// </summary>
public class GameCommandTests
{
    [Fact]
    public void Execute_WhenCommandThrows_PublishesErrorObservation()
    {
        var user = Substitute.For<IUser>();
        user.Id.Returns(Guid.NewGuid());
        var player = Substitute.For<IPlayer>();
        player.User.Returns(user);

        var context = Substitute.For<IGameContext>();
        context.UserToPlayer.Returns(new Dictionary<IUser, IPlayer> { { user, player } });

        var publisher = Substitute.For<IAgentObservationPublisher>();
        var utils = new GameCommandUtils(
            context, Substitute.For<IGameRound>(), Substitute.For<IServiceProvider>(),
            Substitute.For<ISnapshotSender>(), Substitute.For<ISnapshotDiffGuard>(),
            Substitute.For<ILogger<GameCommandUtils>>(), Substitute.For<ISessionLogger>(),
            new MatchStatsTracker(), publisher);

        var response = (EmptyResponse)new ThrowingCommand(utils).Execute(user, new SharedGameAction.Open());

        response.HasError.Should().BeTrue();
        response.Message.Should().Be("An error occurred while processing the command.");
        publisher.Received(1).Publish(user.Id, "action", true, response.Message, false);
    }

    private class ThrowingCommand(GameCommandUtils utils) : GameCommand<SharedGameAction.Open>(utils)
    {
        protected override EmptyResponse Execute(Context context, SharedGameAction.Open request)
            => throw new InvalidOperationException("Turns cannot be less than zero.");
    }
}
