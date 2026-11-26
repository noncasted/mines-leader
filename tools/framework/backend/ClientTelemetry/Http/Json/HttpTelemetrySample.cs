namespace Game.ClientTelemetry;

// JSON has no NaN: Empty lists the slots of Values without a value (their numbers there are ignored), an index out of Values is ignored.
public sealed class HttpTelemetrySample
{
    public long Time { get; init; }
    public double[]? Values { get; init; }
    public int[]? Empty { get; init; }
    public bool Hidden { get; init; }
}
