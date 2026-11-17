namespace Game.ClientTelemetry;

[GenerateSerializer]
public enum TelemetrySessionStatus
{
    Live,
    Ended,

    // No end from the client: its socket closed, or no batch for ClientTelemetryStorageOptions.LostAfter (a gateway gone with the socket).
    Lost
}
