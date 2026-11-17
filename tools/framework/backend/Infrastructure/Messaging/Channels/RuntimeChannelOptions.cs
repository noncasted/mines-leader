using Common;
using Shared;

namespace Infrastructure;

[GrainState(Table = "configs", State = "runtime_channel_config", Lookup = "RuntimeChannelConfig",
    Key = GrainKeyType.String)]
public class RuntimeChannelOptions
{
    public int ObserverKeepAliveMinutes { get; init; } = 3;
    public int CatchUpBufferSize { get; init; } = 1024;
    public int DeliveryTimeoutSeconds { get; init; } = 5;
    public int ResubscribeMinIntervalMs { get; init; } = 10_000; // resubscribe of a listener, after its failures faster
    public int ResubscribeMaxIntervalMs { get; init; } = 60_000; // reached by a listener with stable resubscribes
    public int ResubscribeTimeoutMs { get; init; } = 30_000; // after it a pass of the loop stops waiting for a listener
}
