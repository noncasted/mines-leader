using Common;
using Shared;

namespace Game.ClientTelemetry;

// One row with a fixed key (AddressableState: the name of the state) and no deploy id: the flag survives a new deploy and a restart
// of the coordinator. A row in configs, next to the options that live as long.
[GrainState(Table = "configs", State = StateName, Lookup = "ClientTelemetrySettings", Key = GrainKeyType.String)]
public class ClientTelemetrySettingsState
{
    public const string StateName = "client_telemetry_settings";

    public bool IsEnabled { get; init; }
}
