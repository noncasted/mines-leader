using Infrastructure;

namespace Game.ClientTelemetry;

// The compact states of all the sessions in the memory of a host: the list, the search and the check of a known session.
public interface ITelemetrySessionCollection : IStateCollection<Guid, TelemetrySessionState>;
