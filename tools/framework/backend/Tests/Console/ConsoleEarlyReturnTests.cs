using System.Reflection;
using BlazorBlueprint.Components;
using Cluster.Coordination;
using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Monitoring.Online;
using Console;
using Console.Common;
using Console.Home;
using Console.Infrastructure.Debug;
using Console.Infrastructure.Discovery;
using Console.Infrastructure.Online;
using Framework.Tests.ClientTelemetry;
using Infrastructure;
using Infrastructure.State;
using Microsoft.AspNetCore.Components;
using Shared;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Console;

// A page that leaves its guard with `return` inside an open element throws "A frame of type 'Element' was left unclosed"
// and kills the circuit: every guard state of a page renders.
public class ConsoleEarlyReturnTests
{
    private const string StateTable = "states";

    [Fact]
    public async Task OnlineStats_WithoutData_Renders()
    {
        var html = await Render<OnlineStats>(Services());

        Assert.Contains("No online statistics yet", html);
    }

    [Fact]
    public async Task OnlineWidget_WithoutData_Renders()
    {
        var html = await Render<OnlineWidget>(Services());

        Assert.Contains("no data", html);
    }

    [Fact]
    public async Task Discovery_WithoutServices_Renders()
    {
        var html = await Render<Discovery>(Services());

        Assert.Contains("No services discovered", html);
    }

    [Fact]
    public async Task StateExplorer_WhileLoading_Renders()
    {
        var read = new TaskCompletionSource<string>();
        var storage = Storage(read.Task);

        await using var services = Services().AddSingleton(storage).BuildServiceProvider();
        using var renderer = new ClickingRenderer(services);

        var load = await Load(renderer);

        await storage.Received(1).ReadRawJson(Arg.Any<StateIdentity>());
        read.SetResult("{}");
        await load;
    }

    [Fact]
    public async Task StateExplorer_WhenNotFound_Renders()
    {
        var storage = Storage(Task.FromResult(""));

        await using var services = Services().AddSingleton(storage).BuildServiceProvider();
        using var renderer = new ClickingRenderer(services);

        await await Load(renderer);

        await storage.Received(1).ReadRawJson(Arg.Any<StateIdentity>());
    }

    // The state type and the key as the dropdown and the input of the page leave them, then the handler of the Load button.
    private static async Task<Task> Load(ClickingRenderer renderer)
    {
        var page = await renderer.RenderPage(typeof(StateExplorer), new());

        Field(page, "_selectedTable").SetValue(page, StateTable);
        Field(page, "_grainKey").SetValue(page, "key");

        return renderer.Handle(page, "Load");
    }

    private static FieldInfo Field(IComponent page, string name)
    {
        return page.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    }

    private static IStateStorage Storage(Task<string> read)
    {
        var registry = Substitute.For<IGrainStatesRegistry>();
        registry.All.Returns([new GrainStateInfo { TableName = StateTable, KeyType = GrainKeyType.String, Type = typeof(object), Name = "Any" }]);

        var storage = Substitute.For<IStateStorage>();
        storage.Registry.Returns(registry);
        storage.ReadRawJson(Arg.Any<StateIdentity>()).Returns(read);
        return storage;
    }

    private static ServiceCollection Services()
    {
        var online = Substitute.For<IDeploymentState<OnlineStatsLiveData>>();
        online.Value.Returns(new OnlineStatsLiveData());

        var discovery = Substitute.For<IServiceDiscovery>();
        discovery.Entries.Returns(new Dictionary<Guid, IServiceOverview>());

        var services = new ServiceCollection();
        services.AddBlazorBlueprintComponents();
        services.AddSingleton(online);
        services.AddSingleton(discovery);
        services.AddSingleton(Substitute.For<IConsoleNavigation>());
        services.AddSingleton(Substitute.For<IBrowserTime>());
        services.AddSingleton(Substitute.For<IOrleans>());
        services.AddSingleton(Substitute.For<IDeployContext>());
        services.AddSingleton(Substitute.For<IClusterStartupConfig>());
        services.AddSingleton(Substitute.For<IClipboard>());
        return services;
    }

    private static async Task<string> Render<T>(ServiceCollection collection) where T : IComponent
    {
        await using var services = collection.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.Empty)).ToHtmlString());
    }
}
