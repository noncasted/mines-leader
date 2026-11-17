namespace Console.Infrastructure.BackendTraces;

// A read of a trace: found, gone from the dashboard (it keeps telemetry in memory only) or not read.
public abstract record BackendTraceResult
{
    private BackendTraceResult()
    {
    }

    public sealed record Found(BackendTrace Trace) : BackendTraceResult;

    public sealed record NotFound : BackendTraceResult;

    public sealed record Failed(string Message) : BackendTraceResult;
}
