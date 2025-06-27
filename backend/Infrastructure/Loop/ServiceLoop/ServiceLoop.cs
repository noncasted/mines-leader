using Common;
using Microsoft.Extensions.Hosting;

namespace ServiceLoop;

public class ServiceLoop : BackgroundService
{
    public ServiceLoop(
        ISiloLifecycleSubject siloLifecycle,
        IEnumerable<IOrleansLoopStage> orleans,
        IEnumerable<IMessagingLoopStage> messaging,
        IEnumerable<ISetupLoopStage> setup)
    {
        _siloLifecycle = siloLifecycle;
        _orleans = orleans;
        _messaging = messaging;
        _setup = setup;

        _lifetime = new TerminatedLifetime();
    }
    
    private readonly ISiloLifecycle _siloLifecycle;
    private readonly IEnumerable<IOrleansLoopStage> _orleans;
    private readonly IEnumerable<IMessagingLoopStage> _messaging;
    private readonly IEnumerable<ISetupLoopStage> _setup;
    
    private IReadOnlyLifetime _lifetime;
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _siloLifecycle.Subscribe<ServiceLoop>(ServiceLifecycleStage.Active, _ => OnSiloActive());
        _lifetime = stoppingToken.ToLifetime();
        return Task.CompletedTask;
    }

    private Task OnSiloActive()
    {
        RunStages().NoAwait();
        
        return Task.CompletedTask;
    }

    private async Task RunStages()
    {
        await RunStage(_orleans, listener => listener.OnOrleansStage(_lifetime));
        await RunStage(_messaging, listener => listener.OnMessagingStage(_lifetime));
        await RunStage(_setup, listener => listener.OnSetupStage(_lifetime));
    }
    
    private Task RunStage<T>(IEnumerable<T> entries, Func<T, Task> action)
    {
        return Task.WhenAll(entries.Select(action));
    }
}