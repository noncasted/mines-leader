using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Game.ClientTelemetry;

// The spans of a startup trace, apart from TelemetryStartupState: the collection of the hosts keeps the metadata only.
[GenerateSerializer]
[GrainState(Table = "state_client_telemetry_startup_traces", State = "client_telemetry_startup_trace", Lookup = "ClientTelemetryStartupTrace", Key = GrainKeyType.Guid)]
public class TelemetryStartupTraceState : IDirectStateValue
{
    [Id(0)] [JsonProperty] public IReadOnlyList<TelemetryStartupSpan> Spans { get; private set; } = Array.Empty<TelemetryStartupSpan>();

    public int Version => 0;

    public static TelemetryStartupTraceState Of(TelemetryStartupTrace trace)
    {
        return new TelemetryStartupTraceState { Spans = trace.Spans };
    }
}
