using Shared;

namespace Backend.Matches;

[GenerateSerializer]
public class MatchState
{
    [Id(0)]
    public GameMatchType Type { get; set; }
    
    [Id(1)]
    public Guid Winner { get; set; }
    
    [Id(2)]
    public TimeSpan Time { get; set; }
    
    [Id(3)]
    public DateTime Date { get; set; }

    [Id(4)] public IReadOnlyList<Guid> Participants { get; set; } = new List<Guid>();
    
    public MatchOverview CreateOverview(Guid matchId)
    {
        return new MatchOverview
        {
            Id = matchId,
            Date = Date,
            Participants = Participants.ToList(),
            Winner = Winner,
            Time = Time,
            Type = Type
        };
    }
}