namespace Game.MatchMetrics;

// Which series are read and how they are grouped. A group is always one name; GroupBy are tag keys and the pseudo keys
// "user" (the meta id of the stream, Guid.Empty - the server). Tags - the series having all of them.
public sealed record MatchMetricFilter
{
    public const string UserKey = "user";

    public string? Name { get; init; }
    public Guid? UserId { get; init; }
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> GroupBy { get; init; } = Array.Empty<string>();
}
