namespace Infrastructure.Orleans;

public class GrainDeactivationLocker : ILifecycleObserver
{
    public GrainDeactivationLocker(IGrainContextAccessor contextAccessor)
    {
        var lifecycle = contextAccessor.GrainContext.ObservableLifecycle;
        lifecycle.Subscribe(GrainLifecycleStage.Last, this);
    }

    public Task OnStart(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task OnStop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromMicroseconds(100), cancellationToken);
    }
}