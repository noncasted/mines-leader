using System.Net;
using Microsoft.Extensions.Logging;

namespace Console.Infrastructure.BackendTraces;

// Reads a trace from the HTTP API of the Aspire dashboard. The call is the boundary of the console: a network failure, a timeout or
// an answer other than 200 / 404 is a message of the page, not an exception.
public sealed class BackendTraceReader : IBackendTraceSource
{
    public BackendTraceReader(IHttpClientFactory clients, ILogger<BackendTraceReader> logger)
    {
        _clients = clients;
        _logger = logger;
    }

    public const string HttpClientName = "ConsoleBackendTraces";

    private readonly IHttpClientFactory _clients;
    private readonly ILogger<BackendTraceReader> _logger;

    public async Task<BackendTraceResult> Read(string traceId, CancellationToken cancellation)
    {
        var client = _clients.CreateClient(HttpClientName);
        string json;

        try
        {
            using var response = await client.GetAsync($"api/telemetry/traces/{Uri.EscapeDataString(traceId)}", cancellation);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new BackendTraceResult.NotFound();

            if (!response.IsSuccessStatusCode)
                return Failed(traceId, $"The dashboard answered {(int)response.StatusCode} {response.ReasonPhrase}");

            json = await response.Content.ReadAsStringAsync(cancellation);
        }
        catch (HttpRequestException exception)
        {
            return Failed(traceId, $"The dashboard is not reachable: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellation.IsCancellationRequested)
        {
            return Failed(traceId, $"The dashboard has not answered in {client.Timeout.TotalSeconds:0} s");
        }

        return BackendTraceParser.Parse(traceId, json);
    }

    private BackendTraceResult Failed(string traceId, string message)
    {
        _logger.LogWarning("[Console] Backend trace {TraceId}: {Message}", traceId, message);
        return new BackendTraceResult.Failed(message);
    }
}
