using System.Text.Json;

namespace Console.Infrastructure.BackendTraces.VictoriaTraces;

// The answer of GET /select/jaeger/api/traces/{id} of VictoriaTraces v0.13: {"data": [{"processes": {...}, "spans": [...]}]} in
// the Jaeger JSON. Times are microseconds; the parent is the CHILD_OF reference in the same trace (an OTLP link comes as
// FOLLOWS_FROM to another trace); the scope, the kind and the status come as tags, which are no attributes of the span here.
// A pure function of the text: the spans in the order of the answer, none for a trace without spans; a text of another shape throws.
public static class VictoriaTracesParser
{
    private const ulong _nanosPerMicro = 1_000;
    private const string _scopeTag = "otel.scope.name";
    private const string _errorTag = "error";
    private const string _statusMessageTag = "otel.status_description";

    // The tags VictoriaTraces derives from the fields of an OTLP span and its scope (spanAttributeMap of its model.go).
    private static readonly HashSet<string> _spanFieldTags = [_scopeTag, "otel.scope.version", _errorTag, _statusMessageTag, "span.kind", "w3c.tracestate"];

    public static IReadOnlyList<BackendTraceWireSpan> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty("data").EnumerateArray().SelectMany(Spans).ToArray();
    }

    private static IEnumerable<BackendTraceWireSpan> Spans(JsonElement trace)
    {
        var processes = trace.GetProperty("processes");

        return trace.GetProperty("spans").EnumerateArray().Select(span => Span(span, processes)).ToArray();
    }

    private static BackendTraceWireSpan Span(JsonElement span, JsonElement processes)
    {
        var tags = span.GetProperty("tags").EnumerateArray()
            .Select(tag => KeyValuePair.Create(tag.GetProperty("key").GetString()!, TagValue(tag.GetProperty("value"))))
            .ToArray();
        var start = span.GetProperty("startTime").GetUInt64() * _nanosPerMicro;

        return new BackendTraceWireSpan
        {
            SpanId = span.GetProperty("spanID").GetString()!,
            ParentSpanId = Parent(span),
            Name = span.GetProperty("operationName").GetString()!,
            Service = Service(processes.GetProperty(span.GetProperty("processID").GetString()!)),
            Scope = Tag(tags, _scopeTag) ?? string.Empty,
            StartUnixNano = start,
            EndUnixNano = start + span.GetProperty("duration").GetUInt64() * _nanosPerMicro,
            Attributes = tags.Where(tag => !_spanFieldTags.Contains(tag.Key)).ToArray(),
            Error = Tag(tags, _errorTag) == "true" ? new BackendTraceError(Tag(tags, _statusMessageTag) ?? string.Empty) : null
        };
    }

    private static string? Parent(JsonElement span)
    {
        var traceId = span.GetProperty("traceID").GetString();

        foreach (var reference in span.GetProperty("references").EnumerateArray())
        {
            if (reference.GetProperty("refType").GetString() == "CHILD_OF" && reference.GetProperty("traceID").GetString() == traceId)
                return reference.GetProperty("spanID").GetString()!;
        }

        return null;
    }

    // A resource without service.name has an empty service name here.
    private static string Service(JsonElement process)
    {
        var name = process.GetProperty("serviceName").GetString()!;

        return name.Length == 0 ? BackendTraceWireSpan.UnknownService : name;
    }

    private static string? Tag(IReadOnlyList<KeyValuePair<string, string>> tags, string key)
    {
        return tags.FirstOrDefault(tag => tag.Key == key).Value;
    }

    // v0.13 gives every value as a string; the Jaeger types bool, int64 and float64 read as their JSON.
    private static string TagValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
    }
}
