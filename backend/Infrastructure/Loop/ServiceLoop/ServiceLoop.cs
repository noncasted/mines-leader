using Common;
using Microsoft.Extensions.Hosting;

namespace ServiceLoop;

public class ServiceLoop : BackgroundService
{
    public ServiceLoop(
        IEnumerable<IOrleansLoopStage> orleans,
        IEnumerable<IMessagingLoopStage> messaging,
        IEnumerable<ISetupLoopStage> setup)
    {
        _orleans = orleans;
        _messaging = messaging;
        _setup = setup;
    }

    private readonly IEnumerable<IOrleansLoopStage> _orleans;
    private readonly IEnumerable<IMessagingLoopStage> _messaging;
    private readonly IEnumerable<ISetupLoopStage> _setup;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lifetime = stoppingToken.ToLifetime();
        
        await RunStage(_orleans, listener => listener.OnOrleansStage(lifetime));
        await RunStage(_messaging, listener => listener.OnMessagingStage(lifetime));
        await RunStage(_setup, listener => listener.OnSetupStage(lifetime));
    }

    private Task RunStage<T>(IEnumerable<T> entries, Func<T, Task> action)
    {
        return Task.WhenAll(entries.Select(action));
    }
}