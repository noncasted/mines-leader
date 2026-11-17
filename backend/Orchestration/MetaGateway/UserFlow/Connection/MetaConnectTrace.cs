using System.Diagnostics;

namespace MetaGateway.UserFlow.Connection;

/// <summary>
/// The root span "Meta connect" of one meta socket: from the first frame to the projections sent by OnConnected.
/// It is a trace of its own, not a child of the ASP.NET request activity (that one lives as long as the socket), and
/// links to it. Its trace id goes into the startup trace record of the client.
/// </summary>
public sealed class MetaConnectTrace
{
    private MetaConnectTrace(Activity? request, Activity? root)
    {
        _request = request;
        _root = root;
    }

    private readonly Activity? _request;
    private readonly Activity? _root;

    private bool _isEnded;

    // default when nobody listens to the source.
    public ActivityTraceId TraceId => _root?.TraceId ?? default;

    // Called from the middleware itself (not from an awaited method): the change of Activity.Current stays with it.
    public static MetaConnectTrace Start()
    {
        // The request activity is absent when no listener samples ASP.NET.
        var request = Activity.Current;
        Activity.Current = null;

        ActivityLink[]? links = request == null ? null : [new ActivityLink(request.Context)];

        var root = MetaConnectionTelemetry.Source.StartActivity(
            "Meta connect",
            ActivityKind.Server,
            default(ActivityContext),
            links: links);

        return new MetaConnectTrace(request, root);
    }

    public async Task<T> Phase<T>(string name, Func<Activity?, Task<T>> run)
    {
        using var phase = MetaConnectionTelemetry.Source.StartActivity(name);

        try
        {
            return await run(phase);
        }
        catch (Exception e)
        {
            phase?.AddException(e);
            phase?.SetStatus(ActivityStatusCode.Error, e.Message);
            Fail(e.Message);
            throw;
        }
    }

    public Task Phase(string name, Func<Task> run)
    {
        return Phase(name, async _ => {
            await run();
            return true;
        });
    }

    // Runs a call that starts long-lived work (the reader of the socket) under the request activity, not under the
    // root of the connection that ends before it.
    public void Detached(Action action)
    {
        var current = Activity.Current;
        Activity.Current = _request;

        try
        {
            action();
        }
        finally
        {
            Activity.Current = current;
        }
    }

    public void Fail(string description)
    {
        _root?.SetStatus(ActivityStatusCode.Error, description);
    }

    // Repeated calls do nothing: the middleware ends the root after OnConnected and again on its way out.
    public void End()
    {
        if (_isEnded == true)
            return;

        _isEnded = true;
        _root?.Dispose();
        Activity.Current = _request;
    }
}
