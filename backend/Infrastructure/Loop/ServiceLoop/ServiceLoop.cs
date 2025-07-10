using Common;
using Microsoft.Extensions.Hosting;

namespace ServiceLoop;

public class ServiceLoop : BackgroundService
{
    public ServiceLoop(
        IServiceLoopObserver observer,
        IEnumerable<IOrleansLoopStage> orleans,
        IEnumerable<IMessagingLoopStage> messaging,
        IEnumerable<ISetupLoopStage> setup)
    {
        _observer = observer;
        _orleans = orleans;
        _messaging = messaging;
        _setup = setup;
    }

    private readonly IServiceLoopObserver _observer;
    private readonly IEnumerable<IOrleansLoopStage> _orleans;
    private readonly IEnumerable<IMessagingLoopStage> _messaging;
    private readonly IEnumerable<ISetupLoopStage> _setup;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lifetime = stoppingToken.ToLifetime();

        _observer.IsOrleansStarted.View(lifetime, value =>
        {
            if (value == false)
                return;
            
            Process(lifetime).NoAwait();
        });
        
        return Task.CompletedTask;
    }

    private async Task Process(IReadOnlyLifetime lifetime)
    {
        await RunStage(_orleans, listener => listener.OnOrleansStage(lifetime));
        await RunStage(_messaging, listener => listener.OnMessagingStage(lifetime));
        await RunStage(_setup, listener => listener.OnSetupStage(lifetime));
    }

    private Task RunStage<T>(IEnumerable<T> entries, Func<T, Task> action)
    {
        return Task.WhenAll(entries.Select(action));
    }
}