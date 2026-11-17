using Shared;

namespace Tests.Game;

// An event of the stream of the server: its name and fields.
public record RecordedEvent(string Name, SharedMatchMetrics.MatchEventField[] Fields)
{
    public double Number(string key)
    {
        return Fields.Single(field => field.Key == key).Number;
    }
}
