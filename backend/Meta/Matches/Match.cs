using Cluster.Configs;
using Infrastructure;
using Infrastructure.State;
using Meta.Users;
using Shared;

namespace Meta.Matches;

public class Match : Grain, IMatch
{
    public Match(
        [EventState] EventState<MatchState> state,
        IOrleans orleans,
        IRatingConfig ratingConfig,
        IMatchSummaryCollection summaries)
    {
        _state = state;
        _summaries = summaries;
        _orleans = orleans;
        _ratingConfig = ratingConfig;
    }

    private readonly EventState<MatchState> _state;
    private readonly IOrleans _orleans;
    private readonly IRatingConfig _ratingConfig;
    private readonly IMatchSummaryCollection _summaries;

    private Guid Id => this.GetPrimaryKey();

    public async Task Setup(GameMatchType type, IReadOnlyList<Guid> participants)
    {
        var users = await Task.WhenAll(participants.Select(async p => {
            var handle = _orleans.CreateUserHandle(p);
            var cards = await handle.Deck.GetSelected();
            var user = await handle.Entity.GetState();
            return (UserId: p, Cards: cards, user.Name);
        }));

        var decks = users.ToDictionary(r => r.UserId, r => r.Cards);
        var nicknames = users.ToDictionary(r => r.UserId, r => r.Name);

        var state = await _state.Apply(new MatchSetup(type, DateTime.UtcNow, participants, decks, nicknames));
        await Publish(state);
    }

    public async Task<MatchCompletionSummary> OnComplete(Guid winnerId, Dictionary<Guid, UserStatsDelta> matchStats)
    {
        var endDate = DateTime.UtcNow;

        var ratingOptions = _ratingConfig.Value;

        var winRatingRecord = new UserRatingRecords.Win
        {
            Date = endDate,
            Rating = ratingOptions.WinRating
        };

        var lossRatingRecord = new UserRatingRecords.Loss
        {
            Date = endDate,
            Rating = ratingOptions.LossRating
        };

        var state = await _state.Read();
        var loserId = state.Participants.First(p => p != winnerId);
        var startTime = state.StartDate;

        await _state.Append(new MatchCompleted(winnerId, endDate - startTime),
            new MatchRatingCalculated(new Dictionary<Guid, int>
            {
                [winnerId] = winRatingRecord.GetRating(),
                [loserId] = lossRatingRecord.GetRating()
            }),
            new MatchClosed(endDate));

        await _state.Write();
        await Publish(state);

        var overview = state.CreateOverview(this.GetPrimaryKey());

        var winner = _orleans.CreateUserHandle(winnerId);
        var loser = _orleans.CreateUserHandle(loserId);

        RegisterStatsSideEffect(matchStats, winnerId, won: true);
        RegisterStatsSideEffect(matchStats, loserId, won: false);

        await Task.WhenAll(winner.MatchHistory.Add(overview),
            winner.Rating.AddRecord(winRatingRecord),
            loser.MatchHistory.Add(overview),
            loser.Rating.AddRecord(lossRatingRecord));

        var winnerRating = await winner.Rating.GetTotal();
        var loserRating = await loser.Rating.GetTotal();

        return new MatchCompletionSummary
        {
            Duration = endDate - startTime,
            RatingChanges = new Dictionary<Guid, int>
            {
                [winnerId] = winRatingRecord.GetRating(),
                [loserId] = lossRatingRecord.GetRating()
            },
            Ratings = new Dictionary<Guid, int>
            {
                [winnerId] = winnerRating,
                [loserId] = loserRating
            }
        };
    }

    /// <summary>
    /// Stats go to the player's grain as a separate side effect, one per participant,
    /// so that finishing the match does not depend on their processing.
    /// </summary>
    private static void RegisterStatsSideEffect(
        Dictionary<Guid, UserStatsDelta>? matchStats,
        Guid userId,
        bool won)
    {
        var delta = matchStats != null && matchStats.TryGetValue(userId, out var collected)
            ? collected
            : new UserStatsDelta();

        delta.Add(UserStatType.MatchesPlayed);
        delta.Add(won ? UserStatType.MatchesWon : UserStatType.MatchesLost);

        new UserStatsSideEffect
        {
            UserId = userId,
            Delta = delta
        }.AddToTransaction();
    }

    public async Task Close()
    {
        var state = await _state.Read();

        if (state.IsSetUp == false)
            throw new InvalidOperationException($"[Match] Match {Id} is closed before its setup");

        if (state.IsClosed == true)
            return;

        state = await _state.Apply(new MatchClosed(DateTime.UtcNow));
        await Publish(state);
    }

    public Task<MatchState> GetState()
    {
        return _state.Read();
    }

    private Task Publish(MatchState state)
    {
        return _summaries.OnUpdatedTransactional(Id, state);
    }
}