using Microsoft.Extensions.Logging;

namespace Console.Infrastructure.BackendTraces.VictoriaTraces;

// Reads a trace from the Jaeger query API of VictoriaTraces: GET select/jaeger/api/traces/{id}. The id is the lowercase hex of
// ActivityTraceId (what the startup records keep); VictoriaTraces answers 404 to an unknown id and to an uppercase one.
public sealed class VictoriaTracesReader : IBackendTraceSource
{
    public VictoriaTracesReader(IHttpClientFactory clients, ILogger<VictoriaTracesReader> logger)
    {
        _clients = clients;
        _logger = logger;
    }

    public const string HttpClientName = "ConsoleBackendTraces.VictoriaTraces";

    public static readonly BackendTraceStore Store = new("VictoriaTraces",
        "The trace is not in VictoriaTraces: it is found 20-30 s after its last span and kept until the retention of the store");

    private readonly IHttpClientFactory _clients;
    private readonly ILogger<VictoriaTracesReader> _logger;

    public Task<BackendTraceResult> Read(string traceId, CancellationToken cancellation)
    {
        return BackendTraceHttp.Read(
            _clients.CreateClient(HttpClientName),
            $"select/jaeger/api/traces/{Uri.EscapeDataString(traceId)}",
            traceId,
            Store,
            VictoriaTracesParser.Parse,
            _logger,
            cancellation);
    }
}
