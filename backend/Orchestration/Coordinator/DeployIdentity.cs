using Cluster.Deploy;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.Startup;
using Microsoft.Extensions.Options;

namespace Coordinator;

public class DeployIdentity : BackgroundService
{
    public DeployIdentity(
        IClusterParticipantContext participantContext,
        IDeployContext deployContext,
        IMessaging messaging,
        IOrleans orleans,
        IHostApplicationLifetime hostLifetime,
        IOptions<CoordinatorHealthOptions> options,
        ILogger<DeployIdentity> logger)
    {
        _participantContext = participantContext;
        _deployContext = deployContext;
        _messaging = messaging;
        _orleans = orleans;
        _hostLifetime = hostLifetime;
        _options = options.Value;
        _logger = logger;
    }

    private readonly IClusterParticipantContext _participantContext;
    private readonly IDeployContext _deployContext;
    private readonly IMessaging _messaging;
    private readonly IOrleans _orleans;
    private readonly IHostApplicationLifetime _hostLifetime;
    private readonly CoordinatorHealthOptions _options;
    private readonly ILogger<DeployIdentity> _logger;

    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        var lifetime = new Lifetime();
        cancellation.Register(() => lifetime.Terminate());

        try
        {
            await _participantContext.IsMessagingStarted.WaitTrue(lifetime);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var deployId = Guid.NewGuid();

        _logger.LogInformation("[DeployIdentity] Generated deploy id {DeployId}", deployId);

        var grain = _orleans.GetGrain<IDeployManagement>(deployId);
        await grain.Initialize();

        await _deployContext.Set(deployId, lifetime);

        await _messaging.AddPipeRequestHandler<DeployIdRequest, DeployIdResponse>(lifetime,
            DeployIdPipe.Id,
            _ => Task.FromResult(new DeployIdResponse { DeployId = deployId }));

        _logger.LogInformation("[DeployIdentity] Pipe handler bound, deploy id broadcast");

        HeartbeatLoop(lifetime, grain).NoAwait();
    }

    private async Task HeartbeatLoop(ILifetime lifetime, IDeployManagement grain)
    {
        var consecutiveFailures = 0;

        while (lifetime.IsTerminated == false)
        {
            try
            {
                await grain.Heartbeat();
                consecutiveFailures = 0;
                await Task.Delay(_options.HeartbeatInterval, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                consecutiveFailures++;
                _logger.LogError(e, "[DeployIdentity] Heartbeat iteration failed ({FailureCount}/{MaxFailures})",
                    consecutiveFailures, _options.MaxConsecutiveHeartbeatFailures);

                if (consecutiveFailures >= _options.MaxConsecutiveHeartbeatFailures)
                {
                    // Terminating only the local lifetime would leave a zombie process: alive, but without
                    // the deploy id pipe handler and heartbeat. Stop the whole process so the orchestrator
                    // restarts it and a new deploy epoch begins.
                    _logger.LogCritical("[DeployIdentity] Max consecutive heartbeat failures reached, stopping the application");
                    _hostLifetime.StopApplication();
                    break;
                }

                try
                {
                    await Task.Delay(_options.HeartbeatInterval, lifetime.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
