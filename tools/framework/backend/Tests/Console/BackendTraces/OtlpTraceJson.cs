using System.Text.Json;

namespace Framework.Tests.Console.BackendTraces;

// The OTLP JSON of the dashboard for the tests: resources of spans, a span with its optional fields.
public static class OtlpTraceJson
{
    public static string Answer(params object[] resourceSpans)
    {
        return JsonSerializer.Serialize(new { data = new { resourceSpans }, totalCount = resourceSpans.Length, returnedCount = resourceSpans.Length });
    }

    public static object Resource(string? service, params object[] spans)
    {
        var scopeSpans = new[] { new { scope = new { name = "Test.Scope" }, spans } };

        if (service == null)
            return new { scopeSpans };

        return new { resource = new { attributes = new[] { Attribute("service.name", new { stringValue = service }) } }, scopeSpans };
    }

    // Times in ms from a base, as numbers when asked: the encoding allows both.
    public static Dictionary<string, object> Span(string id, string? parent, double startMs, double endMs, bool numberTimes = false)
    {
        const ulong baseNano = 1_794_927_106_000_000_000;
        var start = baseNano + (ulong)(startMs * 1_000_000);
        var end = baseNano + (ulong)(endMs * 1_000_000);

        var span = new Dictionary<string, object>
        {
            ["traceId"] = "1f3d84b522c4413c10965c00928b3fee",
            ["spanId"] = id,
            ["name"] = $"span {id}",
            ["kind"] = 1,
            ["startTimeUnixNano"] = numberTimes ? start : start.ToString(),
            ["endTimeUnixNano"] = numberTimes ? end : end.ToString()
        };

        if (parent != null)
            span["parentSpanId"] = parent;

        return span;
    }

    public static object Attribute(string key, object value)
    {
        return new { key, value };
    }
}
