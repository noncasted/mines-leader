using FluentAssertions;
using Meta.Matches;
using Meta.Users;
using NSubstitute;
using Shared;
using Tests.Fixtures;
using Xunit;

namespace Tests.Meta.Matches;

// The record of a match (IMatch): the setup with the names and decks, the close with a winner and without one, the
// publication of every change to the records in memory (IMatchSummaryCollection).
[Collection(nameof(OrleansIntegrationCollection))]
public class MatchRecordTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Setup_KeepsNamesAndDecks_Running()
    {
        var (matchId, first, second) = await SetupMatch("Alice", "Bob");

        var state = await GetState(matchId);

        state.IsSetUp.Should().BeTrue();
        state.IsClosed.Should().BeFalse();
        state.ClosedAt.Should().BeNull();
        state.HasWinner.Should().BeFalse();
        state.NicknameOf(first).Should().Be("Alice");
        state.NicknameOf(second).Should().Be("Bob");
        state.ParticipantDecks.Keys.Should().BeEquivalentTo([first, second]);
        state.IsPlayer(first).Should().BeTrue();
        state.IsPlayer(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public async Task OnComplete_ClosesWithWinner()
    {
        var (matchId, winner, loser) = await SetupMatch("Alice", "Bob");

        await RunTransaction(() => GetGrain<IMatch>(matchId).OnComplete(winner, new Dictionary<Guid, UserStatsDelta>()));

        var state = await GetState(matchId);
        state.IsClosed.Should().BeTrue();
        state.Winner.Should().Be(winner);
        state.ClosedAt.Should().Be(state.StartDate + state.Time);
        state.RatingChanges.Keys.Should().BeEquivalentTo([winner, loser]);
    }

    [Fact]
    public async Task Close_WithoutWinner()
    {
        var (matchId, _, _) = await SetupMatch("Alice", "Bob");

        await RunTransaction(() => GetGrain<IMatch>(matchId).Close());

        var state = await GetState(matchId);
        state.IsClosed.Should().BeTrue();
        state.HasWinner.Should().BeFalse();
        state.ClosedAt.Should().Be(state.StartDate + state.Time);
        state.RatingChanges.Should().BeEmpty();
    }

    [Fact]
    public async Task Close_AfterOnComplete_KeepsTheClose()
    {
        var (matchId, winner, _) = await SetupMatch("Alice", "Bob");
        await RunTransaction(() => GetGrain<IMatch>(matchId).OnComplete(winner, new Dictionary<Guid, UserStatsDelta>()));
        var completed = await GetState(matchId);

        await RunTransaction(() => GetGrain<IMatch>(matchId).Close());

        var state = await GetState(matchId);
        state.ClosedAt.Should().Be(completed.ClosedAt);
        state.Winner.Should().Be(winner);
    }

    [Fact]
    public async Task Close_BeforeSetup_Throws()
    {
        var close = () => RunTransaction(() => GetGrain<IMatch>(Guid.NewGuid()).Close());

        await close.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Setup_AfterClose_RunsAgain()
    {
        var (matchId, first, second) = await SetupMatch("Alice", "Bob");
        await RunTransaction(() => GetGrain<IMatch>(matchId).Close());

        await RunTransaction(() => GetGrain<IMatch>(matchId).Setup(GameMatchType.Single, new List<Guid> { first, second }));

        var state = await GetState(matchId);
        state.IsClosed.Should().BeFalse();
        state.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task EveryChange_PublishedToTheSummaries()
    {
        var summaries = GetSiloService<IMatchSummaryCollection>();
        var (matchId, _, _) = await SetupMatch("Alice", "Bob");

        await summaries.Received(1).OnUpdatedTransactional(matchId, Arg.Is<MatchState>(state => state.IsClosed == false));

        await RunTransaction(() => GetGrain<IMatch>(matchId).Close());

        await summaries.Received(1).OnUpdatedTransactional(matchId, Arg.Is<MatchState>(state => state.IsClosed == true));
    }

    private async Task<(Guid MatchId, Guid First, Guid Second)> SetupMatch(string firstName, string secondName)
    {
        var first = await CreateUser(firstName);
        var second = await CreateUser(secondName);

        var matchId = Guid.NewGuid();
        await RunTransaction(() => GetGrain<IMatch>(matchId).Setup(GameMatchType.LastManStandingTurnBased, new List<Guid> { first, second }));

        return (matchId, first, second);
    }

    private async Task<Guid> CreateUser(string name)
    {
        var userId = Guid.NewGuid();

        await RunTransaction(async () => {
            await GetGrain<IUser>(userId).Initialize();
            await GetGrain<IUser>(userId).SetName(name);
            await GetGrain<IUserDeck>(userId).Initialize();
        });

        return userId;
    }

    private async Task<MatchState> GetState(Guid matchId)
    {
        MatchState? state = null;
        await RunTransaction(async () => state = await GetGrain<IMatch>(matchId).GetState());
        return state!;
    }
}
