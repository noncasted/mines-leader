using Shared;

namespace Framework.Tests.MatchMetrics;

// The messages of a client, as the module match-metrics sends them.
public static class MatchMetricsWire
{
    public static SharedMatchMetrics.Open Open(Guid matchId, Guid writerId)
    {
        return new SharedMatchMetrics.Open { MatchId = matchId, WriterId = writerId, Kind = SharedMatchMetrics.MatchStreamKind.Client };
    }

    public static SharedMatchMetrics.MatchSeriesDecl Series(int slot, string name, SharedMatchMetrics.MatchSeriesKind kind, params (string Key, string Value)[] tags)
    {
        return new SharedMatchMetrics.MatchSeriesDecl
        {
            Slot = slot,
            Name = name,
            Kind = kind,
            TagKeys = tags.Select(tag => tag.Key).ToArray(),
            TagValues = tags.Select(tag => tag.Value).ToArray()
        };
    }

    public static SharedMatchMetrics.MatchChunk Chunk(int slot, int start, params float[] values)
    {
        return new SharedMatchMetrics.MatchChunk { Slot = slot, Start = start, AgeMs = 1000, Values = values };
    }

    public static SharedMatchMetrics.MatchEvent Event(string name, float time, params SharedMatchMetrics.MatchEventField[] fields)
    {
        return new SharedMatchMetrics.MatchEvent { Name = name, Time = time, AgeMs = 500, Fields = fields };
    }

    public static SharedMatchMetrics.Batch Batch(
        Guid streamId,
        int seq,
        SharedMatchMetrics.MatchSeriesDecl[]? series = null,
        SharedMatchMetrics.MatchChunk[]? chunks = null,
        SharedMatchMetrics.MatchEvent[]? events = null)
    {
        return new SharedMatchMetrics.Batch
        {
            StreamId = streamId,
            Seq = seq,
            Series = series ?? [],
            Chunks = chunks ?? [],
            Events = events ?? []
        };
    }
}
