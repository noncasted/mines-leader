using Console.Infrastructure.ClientTelemetry;
using Game.ClientTelemetry;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

public class TelemetryConsoleStartupTests
{
    private static TelemetryStartupTree Tree(long alloc = 0)
    {
        return new TelemetryStartupTree(TelemetryStartupAnalysis.Of(
        [
            new TelemetryStartupSpan(-1, "root", 0, 100, 1, false, alloc),
            new TelemetryStartupSpan(0, "late", 50, 10, 1, false, 0),
            new TelemetryStartupSpan(0, "early", 10, 20, 1, false, 0),
            new TelemetryStartupSpan(2, "leaf", 12, 5, 1, true, 0),
            new TelemetryStartupSpan(-1, "second", 100, 300, 300, false, 0)
        ]), 200);
    }

    [Fact]
    public void Rows_GoUnderTheirParents_ByStart_WithoutTheCollapsedOnes()
    {
        var tree = Tree();

        Assert.Equal(["root", "second"], tree.Visible(new HashSet<int>()).Select(span => span.Name));
        Assert.Equal(["root", "early", "late", "second"], tree.Visible(new HashSet<int>(tree.Roots)).Select(span => span.Name));
        Assert.Equal(["root", "early", "leaf", "late", "second"], tree.Visible(new HashSet<int>(tree.All)).Select(span => span.Name));
    }

    [Fact]
    public void AVeryDeepTree_NeedsNoRecursion()
    {
        var spans = Enumerable.Range(0, ClientTelemetryLimits.MaxStartupSpans).Select(index => new TelemetryStartupSpan(index - 1, "a", 0, 1, 0, false, 0)).ToArray();
        var tree = new TelemetryStartupTree(TelemetryStartupAnalysis.Of(spans), 1);

        Assert.Equal(spans.Length, tree.Visible(new HashSet<int>(tree.All)).Count);
    }

    [Fact]
    public void LongestOwn_ComesFirst_AndTheScaleHoldsEverySpan()
    {
        var tree = Tree();

        Assert.Equal(["second", "root"], tree.LongestOwn(2).Select(span => span.Name));
        Assert.Equal(400, tree.ScaleMs);
    }

    [Fact]
    public void TheAllocations_StandOnlyWhenMeasured()
    {
        Assert.False(Tree().HasAllocations);
        Assert.True(Tree(alloc: 1).HasAllocations);
    }

    [Fact]
    public async Task ARow_DrawsItsBarOnTheScale_AndMarksTheUnfinished()
    {
        var tree = Tree();

        var html = await Render(tree.Spans[3], tree.ScaleMs, showAllocations: false);

        Assert.Contains("x=\"30\"", html);
        Assert.Contains("width=\"12.5\"", html);
        Assert.Contains("unfinished", html);
        Assert.DoesNotContain("0 B", html);
        Assert.Contains("0 B", await Render(tree.Spans[3], tree.ScaleMs, showAllocations: true));
    }

    [Fact]
    public void Milliseconds_AreToldBySize()
    {
        Assert.Equal("184.9 ms", TelemetryValueFormat.Milliseconds(184.94));
        Assert.Equal("54.34 s", TelemetryValueFormat.Milliseconds(54337.09));
    }

    private static async Task<string> Render(TelemetryStartupSpanFacts span, double scale, bool showAllocations)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<TelemetryStartupRow>(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(TelemetryStartupRow.Span)] = span,
            [nameof(TelemetryStartupRow.ScaleMs)] = scale,
            [nameof(TelemetryStartupRow.ShowAllocations)] = showAllocations
        }))).ToHtmlString());
    }
}
