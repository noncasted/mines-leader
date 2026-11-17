using Infrastructure;

namespace Game.ClientTelemetry;

// The metadata of all the startup traces in the memory of a host: the list and the sweep.
public interface ITelemetryStartupCollection : IStateCollection<Guid, TelemetryStartupState>;
