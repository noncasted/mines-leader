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
    public void Process_AppliesFixtureAfterRestoreCards()
    {
        var text = File.ReadAllText(FindRoundSource());
        var decksAt = text.IndexOf("AgentMatchFixtureApplier.ApplyDecks", StringComparison.Ordinal);
        var restoreAt = text.IndexOf("_players.RestoreCards(player, snapshot)", StringComparison.Ordinal);
        var applyAt = text.IndexOf("AgentMatchFixtureApplier.Apply(_gameContext", StringComparison.Ordinal);
        var startedAt = text.IndexOf("snapshot.RecordGameStarted", StringComparison.Ordinal);

        decksAt.Should().BeGreaterThan(0);
        restoreAt.Should().BeGreaterThan(decksAt);
        applyAt.Should().BeGreaterThan(restoreAt);
        startedAt.Should().BeGreaterThan(applyAt);
        text.Should().Contain("ResolvePreviousPlayer");
    }

    [Fact]
    public void ProcessRound_PublishesOpponentTurnAfterRestore()
    {
        var text = File.ReadAllText(FindRoundSource());
        var processAt = text.IndexOf("private async Task ProcessRound", StringComparison.Ordinal);
        processAt.Should().BeGreaterThan(0);

        var restoreAt = text.IndexOf("player.Moves.Restore", processAt, StringComparison.Ordinal);
        var opponentTurnAt = text.IndexOf("\"opponent_turn\"", processAt, StringComparison.Ordinal);
        restoreAt.Should().BeGreaterThan(processAt);
        opponentTurnAt.Should().BeGreaterThan(restoreAt);

        var afterRound = text.IndexOf("await ProcessRound(lifetime, nextPlayer)", StringComparison.Ordinal);
        afterRound.Should().BeGreaterThan(0);
        afterRound.Should().BeLessThan(processAt);
        text.Substring(afterRound, processAt - afterRound).Should().NotContain("opponent_turn");
    }

    [Theory]
    [InlineData("LastManStandingRound.cs")]
    [InlineData("LastManStandingTurnBasedRound.cs")]
    [InlineData("TimeLimitedRound.cs")]
    public void RoundEnd_GrowsManaThroughBuff(string fileName)
    {
        var source = Path.Combine(Path.GetDirectoryName(FindRoundSource())!, fileName);
        var text = File.ReadAllText(source);

        text.Should().Contain("PlayerBaseBuffs.GrowMana(player, endSnapshot, ModeOptions.MaxManaCap)");
        text.Should().NotContain("Mana.SetMax(endSnapshot");
    }

    [Fact]
    public void BotDelay_TurnBased_SkipsOnlyRoundPadding()
    {
        BotTurnTiming.IsTurnBased(GameMatchType.LastManStandingTurnBased).Should().BeTrue();
        BotTurnTiming.ShouldSkipRoundPadding(GameMatchType.LastManStandingTurnBased).Should().BeTrue();
        BotTurnTiming.ShouldSkipRoundPadding(GameMatchType.LastManStanding).Should().BeFalse();
        BotTurnTiming.ShouldSkipRoundPadding(GameMatchType.TimeLimited).Should().BeFalse();
        BotTurnTiming.ShouldSkipRoundPadding(GameMatchType.Single).Should().BeFalse();
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
    public void ShouldSkipRoundPadding_OnlyTurnBased()
    {
        BotTurnTiming.ShouldSkipRoundPadding(GameMatchType.LastManStandingTurnBased).Should().BeTrue();
        BotTurnTiming.ShouldSkipRoundPadding(GameMatchType.LastManStanding).Should().BeFalse();
        BotTurnTiming.ShouldSkipRoundPadding(GameMatchType.TimeLimited).Should().BeFalse();
        BotTurnTiming.ShouldSkipRoundPadding(GameMatchType.Single).Should().BeFalse();
    }
}
