using Microsoft.Extensions.Logging;

namespace Console.Infrastructure.BackendTraces.AspireDashboard;

// Reads a trace from the HTTP API of the Aspire dashboard: GET api/telemetry/traces/{id} with the x-api-key of the options.
public sealed class AspireDashboardTraceReader : IBackendTraceSource
{
    public AspireDashboardTraceReader(IHttpClientFactory clients, ILogger<AspireDashboardTraceReader> logger)
    {
        _clients = clients;
        _logger = logger;
    }

    public const string HttpClientName = "ConsoleBackendTraces.AspireDashboard";

    public static readonly BackendTraceStore Store = new("The Aspire dashboard",
        "The trace is no longer in the Aspire dashboard (it keeps telemetry in memory only)");

    private readonly IHttpClientFactory _clients;
    private readonly ILogger<AspireDashboardTraceReader> _logger;

    public Task<BackendTraceResult> Read(string traceId, CancellationToken cancellation)
    {
        return BackendTraceHttp.Read(
            _clients.CreateClient(HttpClientName),
            $"api/telemetry/traces/{Uri.EscapeDataString(traceId)}",
            traceId,
            Store,
            AspireDashboardTraceParser.Parse,
            _logger,
            cancellation);
    }
}
