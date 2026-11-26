using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Game.ClientTelemetry;

// The metadata of a startup trace, for the list. ReceivedAt is the time of this server at the first message: a client tells no clock.
[GenerateSerializer]
[GrainState(Table = "state_client_telemetry_startups", State = "client_telemetry_startup", Lookup = "ClientTelemetryStartup", Key = GrainKeyType.Guid)]
public class TelemetryStartupState : IDirectStateValue
{
    [Id(0)] [JsonProperty] public Guid Id { get; private set; }
    [Id(1)] [JsonProperty] public bool IsStored { get; private set; }
    [Id(2)] [JsonProperty] public TelemetryDevice Device { get; private set; } = new();
    [Id(3)] [JsonProperty] public string Name { get; private set; } = string.Empty;
    [Id(4)] [JsonProperty] public bool Final { get; private set; }
    [Id(5)] [JsonProperty] public DateTime ReceivedAt { get; private set; }
    [Id(6)] [JsonProperty] public DateTime UpdatedAt { get; private set; }
    [Id(7)] [JsonProperty] public double DurationMs { get; private set; }
    [Id(8)] [JsonProperty] public int Frames { get; private set; }
    [Id(9)] [JsonProperty] public int SpanCount { get; private set; }
    [Id(10)] [JsonProperty] public int Depth { get; private set; }
    [Id(11)] [JsonProperty] public int UnfinishedSpans { get; private set; }
    [Id(12)] [JsonProperty] public int DroppedSpans { get; private set; }
    [Id(13)] [JsonProperty] public int RejectedSpans { get; private set; }
    [Id(14)] [JsonProperty] public int CutNames { get; private set; }

    // The traces of one record come from one socket, so the last one tells the same id as the first.
    [Id(15)] [JsonProperty] public string? BackendTraceId { get; private set; }

    public int Version => 0;

    public void Store(Guid id, TelemetryStartupTrace trace, DateTime now)
    {
        if (!IsStored)
            ReceivedAt = now;

        Id = id;
        IsStored = true;
        Device = trace.Device;
        Name = trace.Name;
        Final = trace.Final;
        UpdatedAt = now;
        DurationMs = trace.DurationMs;
        Frames = trace.Frames;
        SpanCount = trace.Spans.Count;
        Depth = TelemetryStartupAnalysis.Depth(trace.Spans);
        UnfinishedSpans = trace.Spans.Count(span => span.Unfinished);
        DroppedSpans = trace.DroppedSpans;
        RejectedSpans = trace.RejectedSpans;
        CutNames = trace.CutNames;
        BackendTraceId = trace.BackendTraceId;
    }

    public TelemetryStartupState Snapshot()
    {
        return (TelemetryStartupState)MemberwiseClone();
    }

    public TelemetryStartupInfo ToInfo()
    {
        return new TelemetryStartupInfo
        {
            Id = Id,
            Device = Device,
            Name = Name,
            Final = Final,
            ReceivedAt = ReceivedAt,
            UpdatedAt = UpdatedAt,
            DurationMs = DurationMs,
            Frames = Frames,
            SpanCount = SpanCount,
            Depth = Depth,
            UnfinishedSpans = UnfinishedSpans,
            DroppedSpans = DroppedSpans,
            RejectedSpans = RejectedSpans,
            CutNames = CutNames,
            BackendTraceId = BackendTraceId
        };
    }
}
