using FluentAssertions;
using Game.GamePlay.Profiles;
using Shared;
using Xunit;

namespace Tests.Game;

public class LastManStandingTurnBasedRoundTests
{
    [Fact]
    public void GetCardMovesCost_TurnBased_UsesOwnOptions()
    {
        var options = new GameModeOptions
        {
            LastManStanding = new LastManStandingModeOptions { CardMovesCost = 1 },
            LastManStandingTurnBased = new LastManStandingTurnBasedModeOptions { CardMovesCost = 0 },
        };

        options.GetCardMovesCost(GameMatchType.LastManStandingTurnBased).Should().Be(0);
    }

    [Fact]
    public void ProcessRound_NoTimerLoop()
    {
        var source = FindRoundSource();
        File.Exists(source).Should().BeTrue();

        var text = File.ReadAllText(source);
        text.Should().NotContain("TimerCountdown");
        text.Should().NotContain("TimeSpan.FromSeconds(1)");
        text.Should().Contain("await TurnsCountdown()");
    }

    [Fact]
    public void BotDelay_TurnBased_IsInstant()
    {
        BotTurnTiming.ShouldSkipDelay(GameMatchType.LastManStandingTurnBased).Should().BeTrue();
        BotTurnTiming.ShouldSkipDelay(GameMatchType.LastManStanding).Should().BeFalse();
        BotTurnTiming.ShouldSkipDelay(GameMatchType.TimeLimited).Should().BeFalse();
        BotTurnTiming.ShouldSkipDelay(GameMatchType.Single).Should().BeFalse();
    }

    private static string FindRoundSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var fromRepo = Path.Combine(
                directory.FullName,
                "backend",
                "Game",
                "GamePlay",
                "Context",
                "Rounds",
                "LastManStandingTurnBasedRound.cs");

            if (File.Exists(fromRepo))
                return fromRepo;

            var fromBackend = Path.Combine(
                directory.FullName,
                "Game",
                "GamePlay",
                "Context",
                "Rounds",
                "LastManStandingTurnBasedRound.cs");

            if (File.Exists(fromBackend))
                return fromBackend;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("LastManStandingTurnBasedRound.cs");
    }
}

public class BotTurnTimingTests
{
    [Fact]
    public void ShouldSkipDelay_OnlyTurnBased()
    {
        BotTurnTiming.ShouldSkipDelay(GameMatchType.LastManStandingTurnBased).Should().BeTrue();
        BotTurnTiming.ShouldSkipDelay(GameMatchType.LastManStanding).Should().BeFalse();
        BotTurnTiming.ShouldSkipDelay(GameMatchType.TimeLimited).Should().BeFalse();
        BotTurnTiming.ShouldSkipDelay(GameMatchType.Single).Should().BeFalse();
    }
}
