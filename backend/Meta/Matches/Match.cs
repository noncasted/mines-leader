using Cluster.Configs;
using Common;
using Infrastructure;
using Infrastructure.State;
using Meta.Users;
using Shared;

namespace Meta.Matches;

public interface IMatch : IGrainWithGuidKey
{
    [Transaction]
    Task Setup(GameMatchType type, IReadOnlyList<Guid> participants);

    [Transaction]
    Task OnComplete(Guid winnerId, Dictionary<Guid, UserStatsDelta> matchStats);

    [Transaction]
    Task<MatchState> GetState();
}

[GenerateSerializer]
[GrainEventState(State = "match_entity", Lookup = "Match", Key = GrainKeyType.Guid)]
public class MatchState : IEventStateValue
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public GameMatchType Type { get; set; }
    [Id(2)] public Guid Winner { get; set; }
    [Id(3)] public TimeSpan Time { get; set; }
    [Id(4)] public DateTime StartDate { get; set; }
    [Id(5)] public IReadOnlyList<Guid> Participants { get; set; } = new List<Guid>();
    [Id(6)] public Dictionary<Guid, IReadOnlyList<CardType>> ParticipantDecks { get; set; } = new();
    [Id(7)] public Dictionary<Guid, int> RatingChanges { get; set; } = new();

    public int Version => 0;

    public void Apply(MatchSetup e)
    {
        Type = e.Type;
        StartDate = e.StartDate;
        Participants = e.Participants;
        ParticipantDecks = e.ParticipantDecks;
    }

    public void Apply(MatchCompleted e)
    {
        Winner = e.Winner;
        Time = e.Time;
    }

    public void Apply(MatchRatingCalculated e)
    {
        foreach (var change in e.Changes)
            RatingChanges[change.Key] = change.Value;
    }

    public MatchOverview CreateOverview(Guid matchId)
    {
        return new MatchOverview
        {
            Id = matchId,
            Date = StartDate,
            Participants = Participants.ToList(),
            Winner = Winner,
            Time = Time,
            Type = Type
        };
    }
}

public record MatchSetup
(GameMatchType Type,
    DateTime StartDate,
    IReadOnlyList<Guid> Participants,
    Dictionary<Guid, IReadOnlyList<CardType>> ParticipantDecks);

public record MatchCompleted(Guid Winner, TimeSpan Time);

public record MatchRatingCalculated(Dictionary<Guid, int> Changes);

public class Match : Grain, IMatch
{
    public Match(
        [EventState] EventState<MatchState> state,
        IOrleans orleans,
        IRatingConfig ratingConfig)
    {
        _state = state;
        _orleans = orleans;
        _ratingConfig = ratingConfig;
    }

    private readonly EventState<MatchState> _state;
    private readonly IOrleans _orleans;
    private readonly IRatingConfig _ratingConfig;

    public async Task Setup(GameMatchType type, IReadOnlyList<Guid> participants)
    {
        var deckResults = await Task.WhenAll(participants.Select(async p => {
            var cards = await _orleans.CreateUserHandle(p).Deck.GetSelected();
            return (UserId: p, Cards: cards);
        }));

        var decks = deckResults.ToDictionary(r => r.UserId, r => r.Cards);

        await _state.Apply(new MatchSetup(type, DateTime.UtcNow, participants, decks));
    }

    public async Task OnComplete(Guid winnerId, Dictionary<Guid, UserStatsDelta> matchStats)
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
            }));

        await _state.Write();

        var overview = state.CreateOverview(this.GetPrimaryKey());

        var winner = _orleans.CreateUserHandle(winnerId);
        var loser = _orleans.CreateUserHandle(loserId);

        RegisterStatsSideEffect(matchStats, winnerId, won: true);
        RegisterStatsSideEffect(matchStats, loserId, won: false);

        await Task.WhenAll(winner.MatchHistory.Add(overview),
            winner.Rating.AddRecord(winRatingRecord),
            loser.MatchHistory.Add(overview),
            loser.Rating.AddRecord(lossRatingRecord));
    }

    /// <summary>
    /// Статы уезжают в грейн игрока отдельным сайд-эффектом — по одному на участника,
    /// чтобы завершение матча не зависело от их обработки.
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

    public Task<MatchState> GetState()
    {
        return _state.Read();
    }
}