namespace Game.ClientTelemetry;

// What a session of a socket keeps of the startup id its client named. IsRejected: the id was not given on this socket.
internal readonly record struct TelemetryStartupLink(Guid? Id, bool IsRejected);
