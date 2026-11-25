using Cluster.Configs;
using Game.ClientTelemetry;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Orchestration;

// The client telemetry of this game is on by default: the coordinator writes the switch on when its row was never written.
// A row already written (on or off from the Features page of the console) is left as it is, so turning it off survives a deploy.
public class ClientTelemetryDefaultSetup : ICoordinatorSetupCompleted
{
    public ClientTelemetryDefaultSetup(
        IHostEnvironment environment,
        ClientTelemetryDefaultState state,
        ILogger<ClientTelemetryDefaultSetup> logger)
    {
        _environment = environment;
        _state = state;
        _logger = logger;
    }

    private readonly IHostEnvironment _environment;
    private readonly ClientTelemetryDefaultState _state;
    private readonly ILogger<ClientTelemetryDefaultSetup> _logger;

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        return ConfigsInitializer.Init(_environment, "config.clientTelemetry", _state, _logger);
    }
}
