using Internal;

namespace Console.Infrastructure.ClientTelemetry;

// The reads of a live telemetry page. A failed read is kept as the error to show and the next one goes on, also on a page that stops
// reading once it has its data: its data is not there yet.
public sealed class TelemetryRefreshLoop
{
    public TelemetryRefreshLoop(Func<Task> refresh, Func<bool> keepsRefreshing, Func<Task> redraw, TimeSpan interval)
    {
        _refresh = refresh;
        _keepsRefreshing = keepsRefreshing;
        _redraw = redraw;
        _interval = interval;
    }

    private readonly Func<Task> _refresh;
    private readonly Func<bool> _keepsRefreshing;
    private readonly Func<Task> _redraw;
    private readonly TimeSpan _interval;

    public string? Error { get; private set; }

    public async Task Run(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false && (Error != null || _keepsRefreshing()))
        {
            await Task.Delay(_interval, lifetime.Token);
            await Refresh();
            await _redraw();
        }
    }

    public async Task Refresh()
    {
        try
        {
            await _refresh();
            Error = null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Error = exception.Message;
        }
    }
}
