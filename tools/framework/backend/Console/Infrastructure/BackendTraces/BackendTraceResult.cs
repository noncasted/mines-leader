namespace Console.Infrastructure.BackendTraces;

// A read of a trace: found, not in the store (the message tells why it may be missing there) or not read.
public abstract record BackendTraceResult
{
    private BackendTraceResult()
    {
    }

    public sealed record Found(BackendTrace Trace) : BackendTraceResult;

    public sealed record NotFound(string Message) : BackendTraceResult;

    public sealed record Failed(string Message) : BackendTraceResult;
}
