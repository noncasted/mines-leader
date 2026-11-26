using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Game.ClientTelemetry;

public class TelemetryStartupCollection(
    StateCollectionUtils<Guid, TelemetryStartupState> utils,
    ILogger<TelemetryStartupCollection> logger)
    : StateCollection<Guid, TelemetryStartupState>(utils, logger), ITelemetryStartupCollection;
