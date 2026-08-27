using FluentAssertions;
using Meta.Matches;
using Meta.Users;
using Shared;
using Tests.Fixtures;
using Xunit;

namespace Tests.Meta;

/// <summary>
/// Проверяет цепочку матч → UserStatsSideEffect → IUserStats →
/// InGameAchievementsSideEffect → IUserInGameAchievements.
/// </summary>
[Collection(nameof(SideEffectIntegrationCollection))]
public class MatchStatsSideEffectTests(SideEffectTestFixture fixture)
    : IntegrationTestBase<SideEffectTestFixture>(fixture)
{
    [Fact]
    public async Task OnComplete_DeliversStatsToBothParticipants()
    {
        var (winnerId, loserId, match) = await CompleteMatch(new Dictionary<Guid, UserStatsDelta>());

        (await DrainSideEffectsAsync()).AssertDrainedWithWork();

        var winnerStats = await ReadStats(winnerId);
        winnerStats.Get(UserStatType.MatchesPlayed).Should().Be(1);
        winnerStats.Get(UserStatType.MatchesWon).Should().Be(1);
        winnerStats.Get(UserStatType.MatchesLost).Should().Be(0);

        var loserStats = await ReadStats(loserId);
        loserStats.Get(UserStatType.MatchesPlayed).Should().Be(1);
        loserStats.Get(UserStatType.MatchesWon).Should().Be(0);
        loserStats.Get(UserStatType.MatchesLost).Should().Be(1);

        match.Should().NotBeNull();
    }

    [Fact]
    public async Task OnComplete_CollectedMatchStatsReachTheGrain()
    {
        var winner = Guid.NewGuid();

        var delta = new UserStatsDelta();
        delta.Add(UserStatType.FlagsSet, 12);
        delta.AddCardPlayed(CardGroup.Attack);

        var (winnerId, _, _) = await CompleteMatch(new Dictionary<Guid, UserStatsDelta>(), winner, delta);

        (await DrainSideEffectsAsync()).AssertDrainedWithWork();

        var stats = await ReadStats(winnerId);
        stats.Get(UserStatType.FlagsSet).Should().Be(12);
        stats.GetCardsPlayed(CardGroup.Attack).Should().Be(1);
    }

    [Fact]
    public async Task OnComplete_StatsSideEffectTriggersAchievementEvaluation()
    {
        var winner = Guid.NewGuid();

        var delta = new UserStatsDelta();
        delta.Add(UserStatType.FlagsSet, 10);

        var (winnerId, _, _) = await CompleteMatch(new Dictionary<Guid, UserStatsDelta>(), winner, delta);

        // Один прогон доводит цепочку до конца: статы применяются и тут же
        // ставят в очередь эффект пересчёта ачивок, который добирается тем же drain-ом.
        (await DrainSideEffectsAsync()).AssertDrainedWithWork();

        IReadOnlyList<InGameAchievementEntry>? unlocked = null;

        await RunTransaction(async () => {
            unlocked = await GetGrain<IUserInGameAchievements>(winnerId).GetUnlocked();
        });

        unlocked!.Should().Contain(entry => entry.Type == InGameAchievementType.FlagsSet && entry.Tier == 1);
    }

    private async Task<(Guid Winner, Guid Loser, IMatch Match)> CompleteMatch(
        Dictionary<Guid, UserStatsDelta> matchStats,
        Guid? winnerId = null,
        UserStatsDelta? winnerDelta = null)
    {
        var winner = winnerId ?? Guid.NewGuid();
        var loser = Guid.NewGuid();

        if (winnerDelta != null)
            matchStats[winner] = winnerDelta;

        await RunTransaction(() => GetGrain<IUserDeck>(winner).Initialize());
        await RunTransaction(() => GetGrain<IUserDeck>(loser).Initialize());

        var match = GetGrain<IMatch>(Guid.NewGuid());

        await RunTransaction(() => match.Setup(GameMatchType.Single, new List<Guid> { winner, loser }));
        await RunTransaction(() => match.OnComplete(winner, matchStats));

        return (winner, loser, match);
    }

    private async Task<UserStatsState> ReadStats(Guid userId)
    {
        UserStatsState? state = null;

        await RunTransaction(async () => {
            state = await GetGrain<IUserStats>(userId).GetState();
        });

        return state!;
    }
}
