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
[GrainEventState(State = "match_entity", Lookup = "Match", Key = GrainKeyType.Guid)]
public class MatchAggregate : IEventStateValue
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

public class MatchSetup
{
    public GameMatchType Type { get; set; }
    public DateTime StartDate { get; set; }
    public IReadOnlyList<Guid> Participants { get; set; } = new List<Guid>();
    public Dictionary<Guid, IReadOnlyList<CardType>> ParticipantDecks { get; set; } = new();
}

public class MatchCompleted
{
    public Guid Winner { get; set; }
    public TimeSpan Time { get; set; }
}

public class MatchRatingCalculated
{
    public Dictionary<Guid, int> Changes { get; set; } = new();
}

public class Match : Grain, IMatch
{
    public Match(
        [EventState] EventState<MatchAggregate> state,
        IOrleans orleans,
        IOptions<ProgressionOptions> options,
        IRatingConfig ratingConfig)
    {
        _state = state;
        _orleans = orleans;
        _options = options;
        _ratingConfig = ratingConfig;
    }

    private readonly EventState<MatchAggregate> _state;
    private readonly IOrleans _orleans;
    private readonly IOptions<ProgressionOptions> _options;
    private readonly IRatingConfig _ratingConfig;

    public async Task Setup(GameMatchType type, IReadOnlyList<Guid> participants)
    {
        var deckResults = await Task.WhenAll(participants.Select(async p => {
            var cards = await _orleans.CreateUserHandle(p).Deck.GetSelected();
            return (UserId: p, Cards: cards);
        }));

        var decks = deckResults.ToDictionary(r => r.UserId, r => r.Cards);

        await _state.Read();
        await _state.Append(new MatchSetup
        {
            Type = type,
            StartDate = DateTime.UtcNow,
            Participants = participants,
            ParticipantDecks = decks
        });
        await _state.Write();
    }

    public async Task OnComplete(Guid winnerId)
    {
        await _state.Read();
        var endDate = DateTime.UtcNow;
        var startTime = _state.Value.StartDate;

        await _state.Append(new MatchCompleted
        {
            Winner = winnerId,
            Time = endDate - startTime
        });

        var state = _state.Value;
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

        await _state.Append(new MatchRatingCalculated
        {
            Changes = new Dictionary<Guid, int>
            {
                [winnerId] = winRatingRecord.GetRating(),
                [loserId] = lossRatingRecord.GetRating()
            }
        });
        await _state.Write();

        await Task.WhenAll(winner.MatchHistory.Add(overview),
            winner.Progression.AddRecord(winRecord),
            winner.Rating.AddRecord(winRatingRecord),
            loser.MatchHistory.Add(overview),
            loser.Progression.AddRecord(lossRecord),
            loser.Rating.AddRecord(lossRatingRecord));
    }

    public Task<MatchState> GetState()
    {
        return _state.ReadAndMapToState();
    }
}

[GenerateSerializer]
public class MatchState : IDirectStateValue
{
    [Id(0)] public GameMatchType Type { get; set; }
    [Id(1)] public Guid Winner { get; set; }
    [Id(2)] public TimeSpan Time { get; set; }
    [Id(3)] public DateTime StartDate { get; set; }
    [Id(4)] public IReadOnlyList<Guid> Participants { get; set; } = new List<Guid>();
    [Id(5)] public Dictionary<Guid, IReadOnlyList<CardType>> ParticipantDecks { get; set; } = new();
    [Id(6)] public Dictionary<Guid, int> RatingChanges { get; set; } = new();

    public int Version => 0;
}

public static class MatchEventStateExtensions
{
    public static async Task<MatchState> ReadAndMapToState(this EventState<MatchAggregate> state)
    {
        await state.Read();
        var v = state.Value;
        return new MatchState
        {
            Type = v.Type,
            Winner = v.Winner,
            Time = v.Time,
            StartDate = v.StartDate,
            Participants = v.Participants,
            ParticipantDecks = v.ParticipantDecks,
            RatingChanges = v.RatingChanges
        };
    }
}
