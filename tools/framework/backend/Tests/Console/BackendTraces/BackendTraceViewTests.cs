using BlazorBlueprint.Components;
using Console;
using Console.Infrastructure.BackendTraces;
using Game.ClientTelemetry;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static Framework.Tests.Console.BackendTraces.OtlpTraceJson;
using StartupPage = Console.Infrastructure.ClientTelemetry.TelemetryStartupPage;

namespace Framework.Tests.Console.BackendTraces;

public class BackendTraceViewTests
{
    private const string TraceId = "1f3d84b522c4413c10965c00928b3fee";
    private const string NotFoundText = "no longer in the dashboard";

    private static BackendTraceResult.Found Found()
    {
        var failing = Span("b", "a", 50, 100);
        failing["status"] = new { code = 2, message = "boom" };
        failing["attributes"] = new[] { Attribute("meta.auth.status", new { stringValue = "Ok" }) };

        return Assert.IsType<BackendTraceResult.Found>(BackendTraceParser.Parse(TraceId, Answer(Resource("meta", Span("a", null, 0, 200), failing))));
    }

    [Fact]
    public async Task AFoundTrace_DrawsItsSpansOnItsOwnAxis_WithAttributesAndErrors()
    {
        var html = await RenderSection(new FakeBackendTraceSource(Found()));

        Assert.Contains("span a", html);
        Assert.Contains("span b", html);
        Assert.Contains("meta.auth.status: Ok", html);
        Assert.Contains("error boom", html);
        Assert.Contains("fill: var(--destructive)", html);
        Assert.Contains("x=\"250\"", html);
        Assert.Contains("width=\"250\"", html);
        Assert.Contains("200 ms", html);
    }

    [Fact]
    public async Task ATraceGone_TellsTheDashboardKeepsItInMemory()
    {
        Assert.Contains(NotFoundText, await RenderSection(new FakeBackendTraceSource(new BackendTraceResult.NotFound())));
    }

    [Fact]
    public async Task AFailure_ShowsItsMessage()
    {
        Assert.Contains("The dashboard answered 500", await RenderSection(new FakeBackendTraceSource(new BackendTraceResult.Failed("The dashboard answered 500"))));
    }

    [Fact]
    public async Task TheStartupPage_WithASource_ReadsTheTraceOfItsSocket()
    {
        var source = new FakeBackendTraceSource(new BackendTraceResult.NotFound());

        var html = await RenderStartupPage(services => services.AddSingleton<IBackendTraceSource>(source));

        Assert.Equal([TraceId], source.Reads);
        Assert.Contains(NotFoundText, html);
    }

    [Fact]
    public async Task TheStartupPage_WithoutASource_ShowsTheIdAlone()
    {
        var html = await RenderStartupPage(_ => { });

        Assert.Contains(TraceId, html);
        Assert.DoesNotContain(NotFoundText, html);
    }

    private static async Task<string> RenderSection(IBackendTraceSource source)
    {
        return await Render<BackendTraceSection>(new ServiceCollection().AddBlazorBlueprintComponents(), new Dictionary<string, object?>
        {
            [nameof(BackendTraceSection.Source)] = source,
            [nameof(BackendTraceSection.TraceId)] = TraceId
        });
    }

    private static async Task<string> RenderStartupPage(Action<IServiceCollection> register)
    {
        var id = Guid.NewGuid();
        var reader = Substitute.For<IClientTelemetryReader>();
        reader.GetStartup(id).Returns(new TelemetryStartupInfo
        {
            Id = id,
            Device = new TelemetryDevice(),
            Name = "Startup",
            Final = true,
            ReceivedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DurationMs = 100,
            Frames = 1,
            SpanCount = 0,
            Depth = 0,
            UnfinishedSpans = 0,
            DroppedSpans = 0,
            RejectedSpans = 0,
            CutNames = 0,
            BackendTraceId = TraceId
        });
        reader.GetStartupSpans(id).Returns([]);
        reader.GetSessions(Arg.Any<TelemetrySessionFilter>()).Returns([]);

        var services = new ServiceCollection().AddBlazorBlueprintComponents().AddSingleton(reader).AddSingleton(Substitute.For<IConsoleNavigation>());
        register(services);

        return await Render<StartupPage>(services, new Dictionary<string, object?> { [nameof(StartupPage.Id)] = id });
    }

    private static async Task<string> Render<T>(IServiceCollection services, Dictionary<string, object?> parameters) where T : IComponent
    {
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }
}
