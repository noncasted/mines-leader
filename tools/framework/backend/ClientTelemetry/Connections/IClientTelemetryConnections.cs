using Internal;

namespace Game.ClientTelemetry;

// What the commands of the meta socket of a game call: the commands stay in the game, like its dispatcher and user session.
public interface IClientTelemetryConnections
{
    // The telemetry of the socket with this lifetime, created by its first message. The end of the lifetime closes its session.
    ClientTelemetryConnection Of(IReadOnlyLifetime connection);
}
