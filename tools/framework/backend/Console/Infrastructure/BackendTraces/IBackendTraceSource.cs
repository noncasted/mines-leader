namespace Console.Infrastructure.BackendTraces;

// The backend traces of the console, registered by AddConsoleBackendTraces. Without it the pages show only the id of a trace.
public interface IBackendTraceSource
{
    Task<BackendTraceResult> Read(string traceId, CancellationToken cancellation);
}
