using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Meta.Matches;

// The record of a match, the key is the id of its game session (GameFlow): the type, the participants with their decks
// and names at the setup, the start, the winner and the duration, the rating changes and the close. A match closes with
// a winner (OnComplete) or without one (Close: the round ended without a winner, the session ended). The same value is
// the header of the match in IMatchSummaryCollection. A rematch sets the same record up again: it shows the last round.
[GenerateSerializer]
[GrainEventState(State = "match_entity", Lookup = "Match", Key = GrainKeyType.Guid)]
public class MatchState : IEventStateValue
{
    [Id(0)][JsonProperty] public string Id { get; private set; } = string.Empty;
    [Id(1)][JsonProperty] public GameMatchType Type { get; private set; }
    [Id(2)][JsonProperty] public Guid Winner { get; private set; }
    [Id(3)][JsonProperty] public TimeSpan Time { get; private set; }
    [Id(4)][JsonProperty] public DateTime StartDate { get; private set; }
    [Id(5)][JsonProperty] public IReadOnlyList<Guid> Participants { get; private set; } = new List<Guid>();
    [Id(6)][JsonProperty] public Dictionary<Guid, IReadOnlyList<CardType>> ParticipantDecks { get; private set; } = new();
    [Id(7)][JsonProperty] public Dictionary<Guid, int> RatingChanges { get; private set; } = new();

    // The names of the participants at the setup; a participant renamed later keeps its old name here.
    [Id(8)][JsonProperty] public Dictionary<Guid, string> Nicknames { get; private set; } = new();

    // Null while the match runs.
    [Id(9)][JsonProperty] public DateTime? ClosedAt { get; private set; }

    public int Version => 0;

    public bool IsSetUp => Participants.Count > 0;
    // A record completed before the close was kept has a winner and no ClosedAt.
    public bool IsClosed => ClosedAt != null || HasWinner;
    public bool HasWinner => Winner != Guid.Empty;

    public void Apply(MatchSetup e)
    {
        Type = e.Type;
        StartDate = e.StartDate;
        Participants = e.Participants;
        ParticipantDecks = e.ParticipantDecks;
        Nicknames = e.Nicknames;
        Winner = Guid.Empty;
        Time = TimeSpan.Zero;
        ClosedAt = null;
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

    public void Apply(MatchClosed e)
    {
        ClosedAt = e.At;
        Time = e.At - StartDate;
    }

    public bool IsPlayer(Guid userId)
    {
        return Participants.Contains(userId);
    }

    // A record set up before the names were kept has none: an empty name for each participant.
    public string NicknameOf(Guid userId)
    {
        if (Nicknames.Count == 0 && IsPlayer(userId) == true)
            return string.Empty;

        if (Nicknames.TryGetValue(userId, out var nickname) == false)
            throw new KeyNotFoundException($"[Match] No participant {userId} in match {Id}");

        return nickname;
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
