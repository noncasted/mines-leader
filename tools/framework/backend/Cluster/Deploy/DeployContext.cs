using Internal;
using Microsoft.Extensions.Logging;

namespace Cluster.Deploy;

public interface IDeployContext
{
    Guid DeployId { get; }

    IReadOnlyLifetime DeployLifetime { get; }

    Task Set(Guid deployId, IReadOnlyLifetime parentLifetime);

    // This host was replaced in its deploy (the coordinator of a newer deploy answers the deploy id): the epoch ends
    // and what runs on its lifetime stops, the deploy id stays.
    void Leave();
}

public class DeployContext : IDeployContext
{
    public DeployContext(
        IEnumerable<IDeployAware> subscribers,
        ILogger<DeployContext> logger)
    {
        _subscribers = subscribers.ToArray();
        _logger = logger;
    }

    private readonly IReadOnlyList<IDeployAware> _subscribers;
    private readonly ILogger<DeployContext> _logger;

    // One switch at a time: a switch that came during a slow subscriber of the previous one would otherwise let the
    // previous one go on and hand its deploy to the subscribers after the slow one.
    private readonly SemaphoreSlim _setLock = new(1, 1);

    private ILifetime? _deployLifetime;
    private Guid _deployId;

    public Guid DeployId => _deployId;

    public IReadOnlyLifetime DeployLifetime =>
        _deployLifetime ?? throw new InvalidOperationException("Deploy not initialized");

    public async Task Set(Guid deployId, IReadOnlyLifetime parentLifetime)
    {
        await _setLock.WaitAsync();

        try
        {
            await Switch(deployId, parentLifetime);
        }
        finally
        {
            _setLock.Release();
        }
    }

    // Not under the lock of Set: it only ends the current epoch, and a switch in progress stops at its next subscriber.
    public void Leave()
    {
        if (_deployLifetime == null)
            throw new InvalidOperationException("Deploy not initialized");

        if (_deployLifetime.IsTerminated == true)
            return;

        _logger.LogWarning("[DeployContext] Left deploy {DeployId}", _deployId);
        _deployLifetime.Terminate();
    }

    private async Task Switch(Guid deployId, IReadOnlyLifetime parentLifetime)
    {
        if (_deployId == deployId && _deployLifetime is { IsTerminated: false })
            return;

        _deployLifetime?.Terminate();

        // Child() removes its listener from the parent when the epoch lifetime ends, so repeated
        // epoch switches do not pile up callbacks on the long-lived parent.
        var newLifetime = parentLifetime.Child();

        _deployLifetime = newLifetime;
        _deployId = deployId;

        _logger.LogInformation("[DeployContext] Deploy id set to {DeployId}", deployId);

        foreach (var subscriber in _subscribers)
        {
            // Left (or the host stops) during a slow subscriber: the rest would attach to an epoch that has ended.
            if (newLifetime.IsTerminated == true)
            {
                _logger.LogWarning("[DeployContext] Deploy {DeployId} ended during its switch, {Subscriber} and the next skipped",
                    deployId, subscriber.GetType().Name);
                return;
            }

            try
            {
                await subscriber.OnDeployChanged(deployId, newLifetime);
            }
            catch (Exception e)
            {
                // A half-switched epoch is not usable: end it and let the caller crash the process.
                _logger.LogCritical(e, "[DeployContext] Subscriber {Subscriber} failed on deploy change to {DeployId}",
                    subscriber.GetType().Name, deployId);

                newLifetime.Terminate();
                throw;
            }
        }
    }
}
