using System.Net.Http.Headers;
using System.Text.Json;
using Common.Extensions;
using Framework.Tests.Fixtures;
using Framework.Tests.Security;
using Game.ClientTelemetry;
using Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// A real Kestrel with the head of the pipeline of a gateway (AddBackendMiddleware of the game), a real cluster and database. Only
// the switch is a substitute: what it does is covered in ClientTelemetrySwitchTests.
public abstract class ClientTelemetryEndpointTest : ClientTelemetryStorageTest
{
    protected ClientTelemetryEndpointTest(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    protected const string Origin = "https://game.example";
    protected const string TestToken = "secret";

    private readonly List<TestWebHost> _hosts = new();

    protected IClientTelemetrySwitch Switch { get; } = Substitute.For<IClientTelemetrySwitch>();

    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public override async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
            await host.DisposeAsync();

        await base.DisposeAsync();
    }

    protected void EnableFlag(bool enabled)
    {
        Switch.IsLoaded.Returns(true);
        Switch.IsEnabled.Returns(enabled);
    }

    protected async Task<TestWebHost> StartHost(
        Action<WebApplicationBuilder>? configure = null,
        Action<WebApplication>? map = null,
        bool ownCollections = false)
    {
        var builder = TestWebHost.CreateBuilder("Production", (AllowedOrigins.ConfigurationKey, Origin));

        builder.ConfigureCors();
        Cluster.AddClusterServicesTo(builder);
        builder.AddClientTelemetry(Cluster.Options);
        builder.Services.AddSingleton(Switch);

        // The collections of the silo: a gateway of the test sees what the grain has told at once. A gateway with its own
        // collections learns it only through the queue of their updates, as another host does.
        if (ownCollections == false)
        {
            builder.Services.AddSingleton(Cluster.Collection);
            builder.Services.AddSingleton(Cluster.Startups);
        }

        configure?.Invoke(builder);

        var app = builder.Build();

        if (ownCollections == true)
        {
            await app.Services.GetRequiredService<TelemetrySessionCollection>().OnLocalSetupCompleted(TestLifetime);
            await app.Services.GetRequiredService<TelemetryStartupCollection>().OnLocalSetupCompleted(TestLifetime);
        }

        app.UseAllowedOrigins();
        app.UseRouting();

        app.UseWebSockets();
        app.MapTelemetryMetaSocket();
        app.MapClientTelemetryRead().AddEndpointFilter(RequireToken);
        map?.Invoke(app);

        var host = await TestWebHost.Start(app);
        _hosts.Add(host);
        return host;
    }

    protected static TelemetryStep Hidden(
        Guid session,
        long seq,
        string[]? names = null,
        (long Time, double?[] Values, bool Hidden)[]? samples = null,
        bool ended = false)
    {
        return Step(session, seq, ended, TelemetryWire.Batch(session, names, samples));
    }

    protected static async Task<JsonElement> Get(TestWebHost host, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ClientTelemetryRoutes.Prefix}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestToken);

        using var response = await host.Http.SendAsync(request, Cancel);
        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel)).RootElement.Clone();
    }

    // RequireConsoleToken of the game: ConsoleAccess stays there, the framework gives the point to put it on.
    private static async ValueTask<object?> RequireToken(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.HttpContext.Request.Headers.Authorization.ToString() != $"Bearer {TestToken}")
            return Results.Unauthorized();

        return await next(context);
    }
}
