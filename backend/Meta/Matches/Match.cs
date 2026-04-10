using Cluster.Configs;
using Common;
using Infrastructure;
using Infrastructure.State;
using Meta.Users;
using Microsoft.Extensions.Options;
using Shared;

namespace Meta.Matches;

public interface IMatch : IGrainWithGuidKey
{
    [Transaction]
    Task Setup(GameMatchType type, IReadOnlyList<Guid> participants);

    [Transaction]
    Task OnComplete(Guid winnerId);

    [Transaction]
    Task<MatchState> GetState();
}

[GenerateSerializer]
[GrainState(Table = "state_match_entity", State = "match_entity", Lookup = "Match", Key = GrainKeyType.Guid)]
public class MatchState : IStateValue
{
    [Id(0)] public GameMatchType Type { get; set; }
    [Id(1)] public Guid Winner { get; set; }
    [Id(2)] public TimeSpan Time { get; set; }
    [Id(3)] public DateTime StartDate { get; set; }
    [Id(4)] public IReadOnlyList<Guid> Participants { get; set; } = new List<Guid>();
    [Id(5)] public Dictionary<Guid, IReadOnlyList<CardType>> ParticipantDecks { get; set; } = new();
    [Id(6)] public Dictionary<Guid, int> RatingChanges { get; set; } = new();

    public int Version => 0;

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

public class Match : Grain, IMatch
{
    public Match(
        [State] State<MatchState> state,
        IOrleans orleans,
        IOptions<ProgressionOptions> options,
        IRatingConfig ratingConfig)
    {
        _state = state;
        _orleans = orleans;
        _options = options;
        _ratingConfig = ratingConfig;
    }

    private readonly State<MatchState> _state;
    private readonly IOrleans _orleans;
    private readonly IOptions<ProgressionOptions> _options;
    private readonly IRatingConfig _ratingConfig;

    public async Task Setup(GameMatchType type, IReadOnlyList<Guid> participants)
    {
        var deckResults = await Task.WhenAll(participants.Select(async p => {
            var cards = await _orleans.CreateUserHandle(p).Deck.GetSelected();
            return (UserId: p, Cards: cards);
        }));

        await _state.Write(state => {
            state.Type = type;
            state.StartDate = DateTime.UtcNow;
            state.Participants = participants;

            foreach (var (userId, cards) in deckResults)
                state.ParticipantDecks[userId] = cards;
        });
    }

    public async Task OnComplete(Guid winnerId)
    {
        var endDate = DateTime.UtcNow;

        var state = await _state.Update(state => {
            state.Winner = winnerId;
            state.Time = endDate - state.StartDate;
        });

        var loserId = state.Participants.First(p => p != winnerId);

        var winner = _orleans.CreateUserHandle(winnerId);
        var loser = _orleans.CreateUserHandle(loserId);

        var overview = state.CreateOverview(this.GetPrimaryKey());

        var winRecord = new UserProgressionRecords.Win
        {
            Date = endDate,
            Experience = _options.Value.WinExperience
        };

        var lossRecord = new UserProgressionRecords.Loss
        {
            Date = endDate,
            Experience = _options.Value.LossExperience
        };

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

        await _state.Write(state => {
            state.RatingChanges[winnerId] = winRatingRecord.GetRating();
            state.RatingChanges[loserId] = lossRatingRecord.GetRating();
        });

        await Task.WhenAll(winner.MatchHistory.Add(overview),
            winner.Progression.AddRecord(winRecord),
            winner.Rating.AddRecord(winRatingRecord),
            loser.MatchHistory.Add(overview),
            loser.Progression.AddRecord(lossRecord),
            loser.Rating.AddRecord(lossRatingRecord));
    }

    public Task<MatchState> GetState()
    {
        return _state.ReadValue();
    }
}