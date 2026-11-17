using System.Text.Json;

namespace Console.Infrastructure.BackendTraces;

// The answer of GET /api/telemetry/traces/{id} of the Aspire dashboard: {"data": {"resourceSpans": [...]}} in OTLP JSON. A pure
// function of the text: a trace without spans is not found, a text of another shape throws.
public static class BackendTraceParser
{
    public const string UnknownService = "unknown";

    public static BackendTraceResult Parse(string traceId, string json)
    {
        using var document = JsonDocument.Parse(json);
        var spans = Spans(document.RootElement.GetProperty("data")).ToArray();

        if (spans.Length == 0)
            return new BackendTraceResult.NotFound();

        return new BackendTraceResult.Found(BackendTraceAssembler.Assemble(traceId, spans));
    }

    private static IEnumerable<OtlpSpan> Spans(JsonElement data)
    {
        foreach (var resourceSpans in data.GetProperty("resourceSpans").EnumerateArray())
        {
            var service = Service(resourceSpans);

            foreach (var scopeSpans in resourceSpans.GetProperty("scopeSpans").EnumerateArray())
            {
                var scope = scopeSpans.TryGetProperty("scope", out var scopeElement) ? OtlpJsonValues.OptionalString(scopeElement, "name") : null;

                foreach (var span in scopeSpans.GetProperty("spans").EnumerateArray())
                    yield return Span(span, service, scope ?? string.Empty);
            }
        }
    }

    private static OtlpSpan Span(JsonElement span, string service, string scope)
    {
        var parent = OtlpJsonValues.OptionalString(span, "parentSpanId");

        return new OtlpSpan
        {
            SpanId = span.GetProperty("spanId").GetString()!,
            ParentSpanId = string.IsNullOrEmpty(parent) ? null : parent,
            Name = span.GetProperty("name").GetString()!,
            Service = service,
            Scope = scope,
            StartUnixNano = OtlpJsonValues.UnixNano(span.GetProperty("startTimeUnixNano")),
            EndUnixNano = OtlpJsonValues.UnixNano(span.GetProperty("endTimeUnixNano")),
            Attributes = OtlpJsonValues.Attributes(span),
            Error = OtlpJsonValues.Error(span)
        };
    }

    // The resource is optional in OTLP; a resource without service.name has no service either.
    private static string Service(JsonElement resourceSpans)
    {
        if (!resourceSpans.TryGetProperty("resource", out var resource))
            return UnknownService;

        var name = OtlpJsonValues.Attributes(resource).FirstOrDefault(attribute => attribute.Key == "service.name");

        return name.Value ?? UnknownService;
    }
}
