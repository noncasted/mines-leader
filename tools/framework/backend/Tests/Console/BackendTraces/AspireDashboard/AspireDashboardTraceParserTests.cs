using Console;
using Console.Infrastructure.BackendTraces;
using Console.Infrastructure.BackendTraces.AspireDashboard;
using Xunit;
using static Framework.Tests.Console.BackendTraces.AspireDashboard.OtlpTraceJson;

namespace Framework.Tests.Console.BackendTraces.AspireDashboard;

public class AspireDashboardTraceParserTests
{
    private const string TraceId = "1f3d84b522c4413c10965c00928b3fee";

    private static BackendTrace Found(string json)
    {
        return BackendTraceAssembler.Assemble(TraceId, AspireDashboardTraceParser.Parse(json));
    }

    [Fact]
    public void TheRecordedAnswer_OfTheDashboard_IsATreeOnItsOwnAxis()
    {
        var trace = Found(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Console", "BackendTraces", "AspireDashboard", "aspire_trace.json")));

        Assert.Equal(7, trace.Spans.Count);
        Assert.Equal(555.8993, trace.DurationMs, 4);
        Assert.Equal(DateTime.UnixEpoch.AddTicks(1794927106092282300 / 100), trace.StartUtc);

        var root = trace.Spans[0];
        Assert.Equal(("Meta connect", "meta", "MetaGateway.Connection", SpanTree<BackendTraceSpan>.NoParent, 0), (root.Name, root.Service, root.Scope, root.ParentIndex, root.Depth));
        Assert.Equal(0, root.StartMs);

        var read = trace.Spans.Single(span => span.Name == "Meta read request");
        Assert.Equal(2.0199, read.StartMs, 4);
        Assert.Equal(18.8632, read.DurationMs, 4);

        var auth = trace.Spans.Single(span => span.Name == "Meta auth");
        Assert.Equal([KeyValuePair.Create("meta.auth.guest", "new"), KeyValuePair.Create("meta.auth.status", "Ok")], auth.Attributes);

        var transactions = trace.Spans.Where(span => span.Name == "Transaction.Process").ToArray();
        Assert.All(transactions, span => Assert.Equal(2, span.Depth));
        Assert.Equal(auth.Index, transactions[0].ParentIndex);

        var tree = new SpanTree<BackendTraceSpan>(trace.Spans, span => span.Index, span => span.ParentIndex, span => span.StartMs);
        Assert.Equal(
            ["Meta connect", "Meta read request", "Meta auth", "Transaction.Process", "Meta send response", "Meta projections", "Transaction.Process"],
            tree.Visible(new HashSet<int>(tree.All)).Select(span => span.Name));
    }

    [Fact]
    public void ASpanWithAParentOutsideTheTrace_IsARoot_AndChildrenGoByStart()
    {
        var trace = Found(Answer(Resource("meta",
            Span("b", "a", 30, 40),
            Span("a", null, 0, 100),
            Span("c", "a", 10, 20),
            Span("orphan", "gone", 50, 60))));

        Assert.Equal(["span a", "span c", "span b", "span orphan"], trace.Spans.Select(span => span.Name));
        Assert.Equal([-1, 0, 0, -1], trace.Spans.Select(span => span.ParentIndex));
        Assert.Equal([0, 1, 1, 0], trace.Spans.Select(span => span.Depth));
    }

    [Fact]
    public void Offsets_AndDurations_AreFromTheFirstStart_TimesAsNumbersToo()
    {
        var trace = Found(Answer(Resource("meta", Span("late", null, 25, 75, numberTimes: true), Span("early", null, 5, 10, numberTimes: true))));

        Assert.Equal(70, trace.DurationMs, 3);
        Assert.Equal([0d, 20d], trace.Spans.Select(span => Math.Round(span.StartMs, 3)));
        Assert.Equal([5d, 50d], trace.Spans.Select(span => Math.Round(span.DurationMs, 3)));
        Assert.Equal(trace.StartUtc.AddMilliseconds(20), trace.Spans[1].StartUtc);
    }

    [Fact]
    public void Attributes_OfEveryType_ReadAsText()
    {
        var span = Span("a", null, 0, 1);
        span["attributes"] = new[]
        {
            Attribute("s", new { stringValue = "text" }),
            Attribute("i", new { intValue = "7" }),
            Attribute("n", new { intValue = 8 }),
            Attribute("b", new { boolValue = true }),
            Attribute("d", new { doubleValue = 1.5 }),
            Attribute("a", new { arrayValue = new { values = new[] { new { stringValue = "x" } } } }),
            Attribute("e", new { })
        };

        var attributes = Found(Answer(Resource("meta", span))).Spans[0].Attributes.ToDictionary();

        Assert.Equal("text", attributes["s"]);
        Assert.Equal("7", attributes["i"]);
        Assert.Equal("8", attributes["n"]);
        Assert.Equal("true", attributes["b"]);
        Assert.Equal("1.5", attributes["d"]);
        Assert.Equal("{\"values\":[{\"stringValue\":\"x\"}]}", attributes["a"]);
        Assert.Equal("", attributes["e"]);
    }

    [Fact]
    public void TheErrorStatus_ByNumberOrName_IsAnError_WithItsMessage()
    {
        var byNumber = Span("a", null, 0, 1);
        byNumber["status"] = new { code = 2, message = "boom" };
        var byName = Span("b", null, 1, 2);
        byName["status"] = new { code = "STATUS_CODE_ERROR" };
        var ok = Span("c", null, 2, 3);
        ok["status"] = new { code = 1 };
        var unset = Span("d", null, 3, 4);
        unset["status"] = new { };

        var spans = Found(Answer(Resource("meta", byNumber, byName, ok, unset))).Spans;

        Assert.Equal(new BackendTraceError("boom"), spans[0].Error);
        Assert.Equal(new BackendTraceError(""), spans[1].Error);
        Assert.Null(spans[2].Error);
        Assert.Null(spans[3].Error);
    }

    [Fact]
    public void NoSpans_AreNoSpans()
    {
        Assert.Empty(AspireDashboardTraceParser.Parse(Answer()));
        Assert.Empty(AspireDashboardTraceParser.Parse(Answer(Resource("meta"))));
    }

    [Fact]
    public void EveryResource_GivesItsService_AndOneWithoutAName_IsUnknown()
    {
        var trace = Found(Answer(
            Resource("meta", Span("a", null, 0, 10)),
            Resource("silo", Span("b", "a", 1, 5)),
            Resource(null, Span("c", "b", 2, 3))));

        Assert.Equal(["meta", "silo", BackendTraceWireSpan.UnknownService], trace.Spans.Select(span => span.Service));
        Assert.Equal([0, 1, 2], trace.Spans.Select(span => span.Depth));
    }

    [Fact]
    public void AnAnswerOfAnotherShape_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => AspireDashboardTraceParser.Parse("{}"));
    }

    [Fact]
    public void ACycleOfParents_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Found(Answer(Resource("meta", Span("a", "b", 0, 1), Span("b", "a", 1, 2)))));
    }
}
