using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Game.ClientTelemetry;

public class TelemetrySessionCollection(
    StateCollectionUtils<Guid, TelemetrySessionState> utils,
    ILogger<TelemetrySessionCollection> logger)
    : StateCollection<Guid, TelemetrySessionState>(utils, logger), ITelemetrySessionCollection;
