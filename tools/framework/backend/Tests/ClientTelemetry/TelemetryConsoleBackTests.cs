using BlazorBlueprint.Components;
using Console;
using Console.Common;
using Console.Infrastructure.ClientTelemetry;
using Game.ClientTelemetry;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using StartupPage = Console.Infrastructure.ClientTelemetry.TelemetryStartupPage;

namespace Framework.Tests.ClientTelemetry;

// The arrow of a page goes one level up, not always to the home page.
public class TelemetryConsoleBackTests
{
    [Fact]
    public async Task Header_WithoutATarget_GoesHome()
    {
        await AssertBack(typeof(PageHeader), new() { [nameof(PageHeader.Title)] = "Any" }, ConsoleConstants.Pages.Home);
    }

    [Fact]
    public async Task SessionList_GoesHome()
    {
        await AssertBack(typeof(TelemetrySessions), new(), ConsoleConstants.Pages.Home);
    }

    [Fact]
    public async Task Session_GoesToTheSessionList()
    {
        await AssertBack(typeof(TelemetrySessionPage), new() { [nameof(TelemetrySessionPage.Id)] = Guid.NewGuid() }, ConsoleConstants.Pages.ClientTelemetry);
    }

    [Fact]
    public async Task ErrorList_GoesToTheSessionList()
    {
        await AssertBack(typeof(TelemetryErrors), new(), ConsoleConstants.Pages.ClientTelemetry);
    }

    [Fact]
    public async Task ErrorGroup_GoesToTheErrorList()
    {
        await AssertBack(typeof(TelemetryErrorGroupPage), new() { [nameof(TelemetryErrorGroupPage.Fingerprint)] = "any" }, ConsoleConstants.Pages.ClientTelemetryErrors);
    }

    [Fact]
    public async Task StartupList_GoesToTheSessionList()
    {
        await AssertBack(typeof(TelemetryStartups), new(), ConsoleConstants.Pages.ClientTelemetry);
    }

    [Fact]
    public async Task Startup_GoesToTheStartupList()
    {
        await AssertBack(typeof(StartupPage), new() { [nameof(StartupPage.Id)] = Guid.NewGuid() }, ConsoleConstants.Pages.ClientTelemetryStartups);
    }

    private static async Task AssertBack(Type page, Dictionary<string, object?> parameters, string expected)
    {
        var navigation = Substitute.For<IConsoleNavigation>();

        await using var services = new ServiceCollection()
            .AddBlazorBlueprintComponents()
            .AddSingleton(navigation)
            .AddSingleton<IClientTelemetryReader>(new FakeTelemetryReader())
            .BuildServiceProvider();
        using var renderer = new ClickingRenderer(services);

        await renderer.RenderPage(page, parameters);
        await renderer.ClickFirstButton();

        navigation.Received(1).NavigateTo(expected);
    }
}
