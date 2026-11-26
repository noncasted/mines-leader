using Infrastructure.State;
using Newtonsoft.Json;
using Common;
using Shared;

namespace Game.ClientTelemetry;

// The metadata and the compact result of a session, for the list and the search. Written at the start and at the close. Holds
// no address and no user agent: Device is the closed list of the contract.
[GenerateSerializer]
[GrainState(Table = "state_client_telemetry_sessions", State = "client_telemetry_session", Lookup = "ClientTelemetrySession", Key = GrainKeyType.Guid)]
public class TelemetrySessionState : IDirectStateValue
{
    [Id(0)] [JsonProperty] public Guid Id { get; private set; }
    [Id(1)] [JsonProperty] public bool IsStarted { get; private set; }
    [Id(2)] [JsonProperty] public TelemetryDevice Device { get; private set; } = new();
    [Id(3)] [JsonProperty] public DateTime StartedAt { get; private set; }
    [Id(4)] [JsonProperty] public bool Closed { get; private set; }
    [Id(5)] [JsonProperty] public TelemetrySessionStatus Status { get; private set; } = TelemetrySessionStatus.Live;
    [Id(6)] [JsonProperty] public DateTime? ClosedAt { get; private set; }
    [Id(7)] [JsonProperty] public long LastSeq { get; private set; }
    [Id(8)] [JsonProperty] public DateTime? LastSampleAt { get; private set; }
    [Id(9)] [JsonProperty] public IReadOnlyList<TelemetryMetricName> Names { get; private set; } = Array.Empty<TelemetryMetricName>();
    [Id(10)] [JsonProperty] public int NameSlots { get; private set; }
    [Id(11)] [JsonProperty] public TelemetrySessionDigest? Digest { get; private set; }

    // Named by the game, kept from the start. A row written before the tags reads as an empty list.
    [Id(12)] [JsonProperty] public IReadOnlyList<string> Tags { get; private set; } = Array.Empty<string>();

    // The startup trace of the client of this session; null: none, or a row written before the traces.
    [Id(13)] [JsonProperty] public Guid? StartupId { get; private set; }

    // The session of the same client before this one that did not close there, as the client reported it; null: none.
    [Id(14)] [JsonProperty] public TelemetryPreviousSession? Previous { get; private set; }

    public int Version => 0;

    public void Open(Guid id, TelemetryDevice device, IReadOnlyList<string> tags, Guid? startupId, TelemetryPreviousSession? previous, DateTime now)
    {
        Previous = previous;
        Id = id;
        IsStarted = true;
        Device = device;
        Tags = tags;
        StartupId = startupId;
        StartedAt = now;
    }

    public void Close(TelemetrySessionStatus status, DateTime now, TelemetrySessionBuffer buffer)
    {
        LastSeq = buffer.LastSeq;
        LastSampleAt = buffer.LastSampleAt;
        Names = buffer.Names;
        NameSlots = buffer.NameSlots;
        Digest = TelemetrySummaryCalculator.Digest(buffer);
        MarkClosed(status, now);
    }

    public void CloseWithoutData(DateTime now)
    {
        MarkClosed(TelemetrySessionStatus.Lost, now);
    }

    public TelemetrySessionState Snapshot()
    {
        return (TelemetrySessionState)MemberwiseClone();
    }

    public TelemetrySessionInfo ToInfo()
    {
        return new TelemetrySessionInfo
        {
            Id = Id,
            Status = Status,
            Device = Device,
            Tags = Tags,
            StartupId = StartupId,
            Previous = Previous,
            StartedAt = StartedAt,
            LastSampleAt = LastSampleAt,
            LastSeq = LastSeq,
            Names = Names,
            NameSlots = NameSlots,
            ClosedAt = ClosedAt,
            Digest = Digest
        };
    }

    private void MarkClosed(TelemetrySessionStatus status, DateTime now)
    {
        Status = status;
        ClosedAt = now;
        Closed = true;
    }
}
