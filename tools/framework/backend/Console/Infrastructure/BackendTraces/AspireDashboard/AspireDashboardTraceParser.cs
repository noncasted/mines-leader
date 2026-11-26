using System.Text.Json;

namespace Console.Infrastructure.BackendTraces.AspireDashboard;

// The answer of GET /api/telemetry/traces/{id} of the Aspire dashboard: {"data": {"resourceSpans": [...]}} in OTLP JSON. A pure
// function of the text: the spans in the order of the answer, none for a trace without spans; a text of another shape throws.
public static class AspireDashboardTraceParser
{
    public static IReadOnlyList<BackendTraceWireSpan> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        return Spans(document.RootElement.GetProperty("data")).ToArray();
    }

    private static IEnumerable<BackendTraceWireSpan> Spans(JsonElement data)
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

    private static BackendTraceWireSpan Span(JsonElement span, string service, string scope)
    {
        var parent = OtlpJsonValues.OptionalString(span, "parentSpanId");

        return new BackendTraceWireSpan
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
            return BackendTraceWireSpan.UnknownService;

        var name = OtlpJsonValues.Attributes(resource).FirstOrDefault(attribute => attribute.Key == "service.name");

        return name.Value ?? BackendTraceWireSpan.UnknownService;
    }
}
