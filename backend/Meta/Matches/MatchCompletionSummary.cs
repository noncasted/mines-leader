namespace Meta.Matches;

/// <summary>Match results for the results screen: computed here, where they are applied to the player grains.</summary>
[GenerateSerializer]
public class MatchCompletionSummary
{
    [Id(0)] public TimeSpan Duration { get; set; }
    [Id(1)] public Dictionary<Guid, int> RatingChanges { get; set; } = new();
    [Id(2)] public Dictionary<Guid, int> Ratings { get; set; } = new();
}
