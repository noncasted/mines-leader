using Console.Infrastructure.BackendTraces;

namespace Framework.Tests.Console.BackendTraces;

// One answer for every trace id; the ids read are kept.
public sealed class FakeBackendTraceSource : IBackendTraceSource
{
    public FakeBackendTraceSource(BackendTraceResult result)
    {
        _result = result;
    }

    private readonly BackendTraceResult _result;
    private readonly List<string> _reads = [];

    public IReadOnlyList<string> Reads => _reads;

    public Task<BackendTraceResult> Read(string traceId, CancellationToken cancellation)
    {
        _reads.Add(traceId);
        return Task.FromResult(_result);
    }
}
