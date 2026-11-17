namespace Game.ClientTelemetry;

// The HTTP intake on the gateway. IdleAfter: a link without a request for this long is a closed socket, its session closes as lost.
// Below ClientTelemetryStorageOptions.LostAfter, so the gateway closes the session before the timer of the grain does. A page in
// the background that stops sending loses its session after it (a socket would keep it open).
public sealed class ClientTelemetryHttpOptions
{
    public TimeSpan IdleAfter { get; init; } = TimeSpan.FromSeconds(60);
}
