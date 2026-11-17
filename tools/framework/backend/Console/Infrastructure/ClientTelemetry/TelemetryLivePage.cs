using Common.Extensions;
using Internal;

namespace Console.Infrastructure.ClientTelemetry;

// A page that reads the telemetry again while its component lives: the loop is the top of a task nobody awaits.
public abstract class TelemetryLivePage : UiComponent
{
    protected TelemetryLivePage()
    {
        _loop = new TelemetryRefreshLoop(Refresh, () => KeepsRefreshing, () => InvokeAsync(StateHasChanged), _interval);
    }

    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(2);

    private readonly TelemetryRefreshLoop _loop;

    private ILifetime? _run;

    protected string? RefreshError => _loop.Error;

    protected virtual bool KeepsRefreshing => true;

    protected abstract Task Refresh();

    protected override Task OnSetup(IReadOnlyLifetime lifetime)
    {
        return Restart();
    }

    // The reads start again: a link to the same route keeps the component and sets a new parameter, the loop of the old
    // one may have ended already (a closed session reads no more).
    protected async Task Restart()
    {
        _run?.Terminate();
        _run = Lifetime.Child();

        await _loop.Refresh();
        _loop.Run(_run).NoAwait();
    }
}
