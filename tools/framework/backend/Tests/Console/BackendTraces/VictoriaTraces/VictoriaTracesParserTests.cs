using Console;
using Console.Infrastructure.BackendTraces;
using Console.Infrastructure.BackendTraces.VictoriaTraces;
using Xunit;
using static Framework.Tests.Console.BackendTraces.VictoriaTraces.JaegerTraceJson;

namespace Framework.Tests.Console.BackendTraces.VictoriaTraces;

public class VictoriaTracesParserTests
{
    private static readonly Dictionary<string, string> _meta = new() { ["p1"] = "meta" };

    private static BackendTrace Found(string json)
    {
        return BackendTraceAssembler.Assemble(TraceId, VictoriaTracesParser.Parse(json));
    }

    // victoria_trace.json is the answer of victoria-traces v0.13.0 to the OTLP trace of victoria_trace_fixture.py.
    [Fact]
    public void TheRecordedAnswer_OfVictoriaTraces_IsATreeOnItsOwnAxis()
    {
        var trace = Found(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Console", "BackendTraces", "VictoriaTraces", "victoria_trace.json")));

        Assert.Equal(6, trace.Spans.Count);
        Assert.Equal(550, trace.DurationMs, 4);
        Assert.Equal(DateTime.UnixEpoch.AddTicks(1795586888091000 * 10), trace.StartUtc);

        var root = trace.Spans[0];
        Assert.Equal(("Meta connect", "meta", "MetaGateway.Connection", SpanTree<BackendTraceSpan>.NoParent, 0), (root.Name, root.Service, root.Scope, root.ParentIndex, root.Depth));
        Assert.Empty(root.Attributes);
        Assert.Null(root.Error);

        var read = trace.Spans.Single(span => span.Name == "Meta read request");
        Assert.Equal((2d, 18d), (read.StartMs, read.DurationMs));
        Assert.Equal(
            [KeyValuePair.Create("meta.request.bytes", "512"), KeyValuePair.Create("meta.request.gzip", "true"), KeyValuePair.Create("meta.request.ratio", "0.25")],
            read.Attributes);

        var auth = trace.Spans.Single(span => span.Name == "Meta auth");
        Assert.Equal([KeyValuePair.Create("meta.auth.guest", "new"), KeyValuePair.Create("meta.auth.status", "Ok")], auth.Attributes);

        var projections = trace.Spans.Single(span => span.Name == "Meta projections");
        Assert.Equal(new BackendTraceError("projection failed"), projections.Error);

        var transactions = trace.Spans.Where(span => span.Name == "Transaction.Process").ToArray();
        Assert.All(transactions, span => Assert.Equal(("silo", "Orleans.Runtime", 2), (span.Service, span.Scope, span.Depth)));
        Assert.Equal([auth.Index, projections.Index], transactions.Select(span => span.ParentIndex));
        Assert.All(transactions, span => Assert.Null(span.Error));

        var tree = new SpanTree<BackendTraceSpan>(trace.Spans, span => span.Index, span => span.ParentIndex, span => span.StartMs);
        Assert.Equal(
            ["Meta connect", "Meta read request", "Meta auth", "Transaction.Process", "Meta projections", "Transaction.Process"],
            tree.Visible(new HashSet<int>(tree.All)).Select(span => span.Name));
    }

    // An OTLP link comes as FOLLOWS_FROM to another trace; a CHILD_OF outside the trace is no parent either.
    [Fact]
    public void OnlyAChildOfInTheTrace_IsAParent()
    {
        var link = new { refType = "FOLLOWS_FROM", spanID = "a", traceID = TraceId };
        var json = Answer(_meta, Span("a", null, 0, 100), Span("b", "a", 10, 20), Span("c", "gone", 30, 40)).Replace(
            "\"references\":[]", $"\"references\":[{System.Text.Json.JsonSerializer.Serialize(link)}]");

        var trace = Found(json);

        Assert.Equal(["span a", "span b", "span c"], trace.Spans.Select(span => span.Name));
        Assert.Equal([-1, 0, -1], trace.Spans.Select(span => span.ParentIndex));
    }

    [Fact]
    public void TheTagsOfTheSpanFields_AreNoAttributes_AndAnErrorWithoutAMessage_IsAnError()
    {
        var span = Span("a", null, 0, 1, "p1", ("span.kind", "server"), ("otel.scope.name", "Scope"), ("otel.scope.version", "1.2.0"),
            ("w3c.tracestate", "vendor=value"), ("error", "true"), ("count", 3), ("flag", false));

        var read = Found(Answer(_meta, span)).Spans.Single();

        Assert.Equal("Scope", read.Scope);
        Assert.Equal(new BackendTraceError(""), read.Error);
        Assert.Equal([KeyValuePair.Create("count", "3"), KeyValuePair.Create("flag", "false")], read.Attributes);
    }

    [Fact]
    public void AnEmptyServiceName_IsUnknown_AndASpanWithoutScope_HasAnEmptyOne()
    {
        var trace = Found(Answer(new Dictionary<string, string> { ["p1"] = "meta", ["p2"] = "" }, Span("a", null, 0, 10), Span("b", "a", 1, 5, "p2")));

        Assert.Equal(["meta", BackendTraceWireSpan.UnknownService], trace.Spans.Select(span => span.Service));
        Assert.Equal(["", ""], trace.Spans.Select(span => span.Scope));
    }

    [Fact]
    public void NoSpans_AreNoSpans()
    {
        Assert.Empty(VictoriaTracesParser.Parse(Answer(_meta)));
    }

    [Fact]
    public void AnAnswerOfAnotherShape_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => VictoriaTracesParser.Parse("{}"));
        Assert.Throws<KeyNotFoundException>(() => VictoriaTracesParser.Parse("{\"data\":[{\"spans\":[]}]}"));
    }
}
