using Game.ClientTelemetry;
using Infrastructure;

namespace Orchestration;

// The row of the client telemetry switch, read on the coordinator only to give it its first value (ClientTelemetryDefaultSetup).
// The switch of the framework (ClientTelemetrySwitch) on every host follows the same row through its channel.
public class ClientTelemetryDefaultState(AddressableStateUtils utils) : AddressableState<ClientTelemetrySettingsState>(utils)
{
}
