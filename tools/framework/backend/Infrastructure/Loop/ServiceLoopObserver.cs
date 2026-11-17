using Internal;

namespace Infrastructure;

public interface IServiceLoopObserver
{
    IViewableProperty<bool> IsOrleansStarted { get; }
}

public class ServiceLoopObserver :
    ILifecycleParticipant<IClusterClientLifecycle>,
    ILifecycleParticipant<ISiloLifecycle>,
    IServiceLoopObserver
{
    private readonly ViewableProperty<bool> _isOrleansStarted = new(false);

    public IViewableProperty<bool> IsOrleansStarted => _isOrleansStarted;

    // Participate is called when the lifecycle is built: started means the Active stage has been reached (the silo
    // joined the cluster, the client connected).
    public void Participate(IClusterClientLifecycle lifecycle)
    {
        lifecycle.Subscribe(nameof(ServiceLoopObserver), ServiceLifecycleStage.Active, OnActive);
    }

    public void Participate(ISiloLifecycle lifecycle)
    {
        lifecycle.Subscribe(nameof(ServiceLoopObserver), ServiceLifecycleStage.Active, OnActive);
    }

    private Task OnActive(CancellationToken cancellationToken)
    {
        _isOrleansStarted.Set(true);
        return Task.CompletedTask;
    }
}
