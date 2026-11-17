using BlazorBlueprint.Components;
using Console;
using Console.Infrastructure.ClientTelemetry;
using Framework.Tests.ClientTelemetry;
using Game.ClientTelemetry;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using static Framework.Tests.ClientTelemetry.TelemetryConsoleSamples;

namespace Framework.Tests.Console;

public class TelemetrySessionPageTests
{
    // The links to the previous and the next session go to the same route: the router keeps the component and sets the new Id.
    [Fact]
    public async Task ANewId_ReadsTheOtherSession()
    {
        var reader = new FakeTelemetryReader();
        var first = SessionOf(TelemetrySessionStatus.Live);
        var second = SessionOf(TelemetrySessionStatus.Live);
        reader.Set(first);
        reader.Set(second);

        await using var services = new ServiceCollection()
            .AddBlazorBlueprintComponents()
            .AddSingleton(Substitute.For<IConsoleNavigation>())
            .AddSingleton<IClientTelemetryReader>(reader)
            .BuildServiceProvider();
        using var renderer = new ClickingRenderer(services);

        await renderer.RenderPage(typeof(TelemetrySessionPage), new() { [nameof(TelemetrySessionPage.Id)] = first.Id });
        await renderer.SetPageParameters(new() { [nameof(TelemetrySessionPage.Id)] = second.Id });

        // The render of new parameters waits for their reads.
        Assert.Contains(second.Id, reader.ErrorReads);
    }
}
