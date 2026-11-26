using Infrastructure.State;
using Newtonsoft.Json;
using Common;
using Shared;

namespace Game.ClientTelemetry;

// Everything a session has sent: written once, at its close. Read only by the host that reads a closed session.
[GenerateSerializer]
[GrainState(Table = "state_client_telemetry_logs", State = "client_telemetry_log", Lookup = "ClientTelemetryLog", Key = GrainKeyType.Guid)]
public class TelemetrySessionLogState : IDirectStateValue
{
    [Id(0)] [JsonProperty] public IReadOnlyList<string?> NameList { get; private set; } = Array.Empty<string?>();
    [Id(1)] [JsonProperty] public IReadOnlyList<string?> UnitList { get; private set; } = Array.Empty<string?>();
    [Id(2)] [JsonProperty] public IReadOnlyList<TelemetryStoredSample> Samples { get; private set; } = Array.Empty<TelemetryStoredSample>();
    [Id(3)] [JsonProperty] public IReadOnlyList<TelemetryStoredError> Errors { get; private set; } = Array.Empty<TelemetryStoredError>();
    [Id(4)] [JsonProperty] public IReadOnlyList<TelemetryStoredMark> Marks { get; private set; } = Array.Empty<TelemetryStoredMark>();
    [Id(5)] [JsonProperty] public long DroppedSamples { get; private set; }
    [Id(6)] [JsonProperty] public long DroppedErrors { get; private set; }
    [Id(7)] [JsonProperty] public long DroppedMarks { get; private set; }
    [Id(8)] [JsonProperty] public long RejectedRequests { get; private set; }

    public int Version => 0;

    public static TelemetrySessionLogState Of(TelemetrySessionBuffer buffer)
    {
        return new TelemetrySessionLogState
        {
            NameList = buffer.NameList,
            UnitList = buffer.UnitList,
            Samples = buffer.Samples.Items,
            Errors = buffer.Errors.Items,
            Marks = buffer.Marks.Items,
            DroppedSamples = buffer.Samples.Dropped,
            DroppedErrors = buffer.Errors.Dropped,
            DroppedMarks = buffer.Marks.Dropped,
            RejectedRequests = buffer.RejectedRequests
        };
    }
}
