using FluentAssertions;
using Infrastructure;
using Meta.Matches;
using Meta.Users;
using Shared;
using Tests.Fixtures;
using Xunit;

namespace Tests.Meta.Telemetry;

// The admission of the metrics collector over the match grain: the participants of the match, the close.
[Collection(nameof(OrleansIntegrationCollection))]
public class MatchMetricsAdmissionTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task UnknownMatch_NotAPlayer_NotClosed()
    {
        var admit = await CreateAdmission().Check(Guid.NewGuid(), Guid.NewGuid());

        admit.IsPlayer.Should().BeFalse();
        admit.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task OpenMatch_ParticipantAdmitted_StrangerNot()
    {
        var (matchId, player, _) = await SetupMatch();
        var admission = CreateAdmission();

        var admit = await admission.Check(matchId, player);
        admit.IsPlayer.Should().BeTrue();
        admit.ClosedAt.Should().BeNull();

        var stranger = await admission.Check(matchId, Guid.NewGuid());
        stranger.IsPlayer.Should().BeFalse();
        stranger.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task CompletedMatch_BothParticipantsAdmitted_CloseIsStartPlusTime()
    {
        var (matchId, winner, loser) = await SetupMatch();
        var match = GetGrain<IMatch>(matchId);

        await RunTransaction(() => match.OnComplete(winner, new Dictionary<Guid, UserStatsDelta>()));

        MatchState? state = null;
        await RunTransaction(async () => {
            state = await match.GetState();
        });

        var admission = CreateAdmission();
        var winnerAdmit = await admission.Check(matchId, winner);
        var loserAdmit = await admission.Check(matchId, loser);

        winnerAdmit.IsPlayer.Should().BeTrue();
        loserAdmit.IsPlayer.Should().BeTrue();
        winnerAdmit.ClosedAt.Should().Be(state!.StartDate + state.Time);
        loserAdmit.ClosedAt.Should().Be(winnerAdmit.ClosedAt);
    }

    [Fact]
    public async Task ClosedWithoutWinner_ParticipantAdmitted_ClosedAtTheClose()
    {
        var (matchId, player, _) = await SetupMatch();
        var match = GetGrain<IMatch>(matchId);

        await RunTransaction(match.Close);

        MatchState? state = null;
        await RunTransaction(async () => {
            state = await match.GetState();
        });

        var admit = await CreateAdmission().Check(matchId, player);

        admit.IsPlayer.Should().BeTrue();
        admit.ClosedAt.Should().NotBeNull();
        admit.ClosedAt.Should().Be(state!.ClosedAt);
    }

    private async Task<(Guid MatchId, Guid First, Guid Second)> SetupMatch()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await RunTransaction(() => GetGrain<IUserDeck>(first).Initialize());
        await RunTransaction(() => GetGrain<IUserDeck>(second).Initialize());

        var matchId = Guid.NewGuid();
        await RunTransaction(() => GetGrain<IMatch>(matchId).Setup(GameMatchType.Single, new List<Guid> { first, second }));

        return (matchId, first, second);
    }

    private MatchMetricsAdmission CreateAdmission()
    {
        return new MatchMetricsAdmission(GetSiloService<IOrleans>());
    }
}
