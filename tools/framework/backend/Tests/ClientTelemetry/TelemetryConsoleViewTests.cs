using Console;
using Console.Infrastructure.ClientTelemetry;
using Game.ClientTelemetry;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Framework.Tests.ClientTelemetry.TelemetryConsoleSamples;

namespace Framework.Tests.ClientTelemetry;

// The markup of the small pieces of the pages, without a circuit.
public class TelemetryConsoleViewTests
{
    [Fact]
    public async Task Row_ShowsEveryTagOfTheSession()
    {
        var html = await Render<TelemetrySessionRow>(new() { [nameof(TelemetrySessionRow.Session)] = SessionOf(tags: ["alpha", "beta.two"]) });

        Assert.Contains(">alpha<", html);
        Assert.Contains(">beta.two<", html);
    }

    [Fact]
    public async Task Row_WithoutTags_ShowsTheUnknownMark()
    {
        var html = await Render<TelemetryTagList>(new() { [nameof(TelemetryTagList.Tags)] = Array.Empty<string>() });

        Assert.Contains($">{TelemetryValueFormat.Unknown}<", html);
    }

    [Fact]
    public async Task LostBadge_SaysItDoesNotProveADeadClient()
    {
        var lost = await Render<TelemetryStatusBadge>(new() { [nameof(TelemetryStatusBadge.Status)] = TelemetrySessionStatus.Lost });
        var ended = await Render<TelemetryStatusBadge>(new() { [nameof(TelemetryStatusBadge.Status)] = TelemetrySessionStatus.Ended });

        Assert.Contains($"title=\"{TelemetrySessionFacts.LostMeaning}\"", lost);
        Assert.Contains("socket", TelemetrySessionFacts.LostMeaning);
        Assert.Contains("timer", TelemetrySessionFacts.LostMeaning);
        Assert.DoesNotContain("title=", ended);
    }

    [Fact]
    public async Task PreviousSession_LinksIt_WithTheLastSampleAndMarksOfTheClient()
    {
        var lost = Guid.NewGuid();
        var previous = new TelemetryPreviousSession(lost, ["menu"], 31_000, 4_000, [new TelemetryMark(30_000, "load.slow", "Group: MusicRunAudio", 5_000), new TelemetryMark(30_500, "webgl.error.out_of_memory")]);

        var html = await Render<TelemetryPreviousSessionView>(new() { [nameof(TelemetryPreviousSessionView.Previous)] = previous });

        Assert.Contains($"href=\"{ConsoleConstants.Pages.ClientTelemetrySession(lost)}\"", html);
        Assert.Contains(">menu<", html);
        Assert.Contains("webgl.error.out_of_memory", html);
        Assert.Contains("Group: MusicRunAudio", html);
        Assert.Contains(TelemetryValueFormat.Milliseconds(5_000), html);
        Assert.Contains(TelemetryValueFormat.Time(31_000), html);
    }

    [Fact]
    public async Task NextSession_SaysTheClientDidNotCloseThisOne()
    {
        var next = Guid.NewGuid();

        var html = await Render<TelemetryNextSessionView>(new() { [nameof(TelemetryNextSessionView.Next)] = new[] { SessionOf(id: next) } });

        Assert.Contains($"href=\"{ConsoleConstants.Pages.ClientTelemetrySession(next)}\"", html);
        Assert.Contains("did not close", html);
    }

    private static async Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }
}
