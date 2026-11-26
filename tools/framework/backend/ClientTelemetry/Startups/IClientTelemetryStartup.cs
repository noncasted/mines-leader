namespace Game.ClientTelemetry;

// The record of one startup trace of a client, its guid issued by the gateway. The only writer of the record.
public interface IClientTelemetryStartup : IGrainWithGuidKey
{
    // A snapshot or the final trace. The final one stays: a snapshot after it changes nothing.
    Task Store(TelemetryStartupTrace trace);

    // The pass of ClientTelemetrySweep: an expired record is deleted.
    Task Sweep();
}
