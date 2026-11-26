using System.Text.Json;

namespace Framework.Tests.Console.BackendTraces.VictoriaTraces;

// The Jaeger JSON of VictoriaTraces for the tests: one trace of processes and spans, a span with its references and tags.
public static class JaegerTraceJson
{
    public const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    public static string Answer(Dictionary<string, string> processes, params object[] spans)
    {
        var data = spans.Length == 0
            ? Array.Empty<object>()
            : [new { processes = processes.ToDictionary(process => process.Key, process => new { serviceName = process.Value, tags = Array.Empty<object>() }), spans, traceID = TraceId }];

        return JsonSerializer.Serialize(new { data, errors = (object?)null, limit = 0, offset = 0, total = data.Length });
    }

    // Times in ms from a base, as the microseconds of Jaeger.
    public static object Span(string id, string? parent, double startMs, double endMs, string process = "p1", params (string Key, object Value)[] tags)
    {
        const ulong baseMicro = 1_795_586_888_091_000;
        var references = parent == null ? [] : new[] { new { refType = "CHILD_OF", spanID = parent, traceID = TraceId } };

        return new
        {
            duration = (ulong)((endMs - startMs) * 1_000),
            logs = Array.Empty<object>(),
            operationName = $"span {id}",
            processID = process,
            references,
            spanID = id,
            startTime = baseMicro + (ulong)(startMs * 1_000),
            tags = tags.Select(tag => new { key = tag.Key, type = tag.Value is string ? "string" : tag.Value is bool ? "bool" : "int64", value = tag.Value }).ToArray(),
            traceID = TraceId,
            warnings = (object?)null
        };
    }
}
