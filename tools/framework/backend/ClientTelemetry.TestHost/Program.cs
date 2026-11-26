using Common.Extensions;
using Framework.ClientTelemetry.TestHost.ConsolePages;
using Framework.ClientTelemetry.TestHost;
using Framework.Tests.ClientTelemetry;
using Framework.Tests.Fixtures;
using Framework.Tests.Security;
using Game.ClientTelemetry;
using Infrastructure;
using Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

// The test cluster writes its logs/ next to the working directory: not into the tree it was started from.
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var arguments = TelemetryHostArguments.Parse(args);
var stats = new TelemetryHostStats();
var lifetime = new Lifetime();

var cluster = new TelemetryHostCluster(arguments.LostAfter);
await cluster.InitializeAsync();

var telemetrySwitch = new ClientTelemetrySwitch(new AddressableStateUtils(cluster.Orleans, cluster.Messaging, NullLoggerFactory.Instance));
await telemetrySwitch.OnOrleansStarted(lifetime);

// The pipeline of ClientTelemetryEndpointTest: the head of a gateway of a game, the meta socket and the reading under the token.
// The circuit of the console opens a socket from the address of this host.
var origins = arguments.ConsoleStyles == null ? "http://localhost" : $"http://localhost,http://127.0.0.1:{arguments.Port}";
var builder = TestWebHost.CreateBuilder("Production", (AllowedOrigins.ConfigurationKey, origins));
builder.WebHost.UseUrls($"http://127.0.0.1:{arguments.Port}");
builder.ConfigureCors();
cluster.AddClusterServicesTo(builder);

if (arguments.NoTelemetry == false)
{
    builder.AddClientTelemetry(cluster.Options);

    // The started switch of this process instead of the one AddClientTelemetry has registered: the Features page lists all of them.
    builder.Services.RemoveAll<IClientTelemetrySwitch>();
    builder.Services.AddSingleton<IClientTelemetrySwitch>(telemetrySwitch);
    builder.Services.AddSingleton(cluster.Collection);
    builder.Services.AddSingleton(cluster.Startups);
}

if (arguments.ConsoleStyles != null)
    await TelemetryHostConsole.Add(builder, cluster.Database.DataSource);

var app = builder.Build();

app.UseAllowedOrigins();
app.UseWebSockets();
app.UseRouting();

if (arguments.ConsoleStyles != null)
    TelemetryHostConsole.Map(app, arguments.ConsoleStyles);

if (arguments.NoTelemetry == false)
{
    app.MapTelemetryMetaSocket(stats.Count);
    app.MapMuteSocket();
    app.MapClientTelemetryRead().AddEndpointFilter(RequireToken);
}

// What the console and the operator do on a real deploy: the Features page changes the same switch.
app.MapPost("/test-host/flag/{enabled:bool}", async (bool enabled) => {
    await telemetrySwitch.Set(enabled);
    return Results.Ok(new { enabled });
}).AddEndpointFilter(RequireToken);

app.MapGet("/test-host/stats", () => Results.Ok(new { messages = stats.Total, byType = stats.Messages }))
   .AddEndpointFilter(RequireToken);

var host = await TestWebHost.Start(app);

if (arguments.Seed)
{
    await telemetrySwitch.Set(true);
    await TelemetryHostSeed.Run(host.Address, arguments.StartupFile);
}

// The address goes out last: whoever waits for it finds everything ready.
if (arguments.AddressFile != null)
    await File.WriteAllTextAsync(arguments.AddressFile, host.Address.ToString());

System.Console.WriteLine($"[telemetry-host] listening on {host.Address} flag={telemetrySwitch.IsEnabled} seeded={arguments.Seed}");

await app.WaitForShutdownAsync();

System.Console.WriteLine($"[telemetry-host] port {host.Address.Port}: {stats.Total} telemetry messages, by type: "
    + string.Join(", ", stats.Messages.Select(pair => $"{pair.Key}={pair.Value}")));

lifetime.Terminate();
await host.DisposeAsync();
await cluster.DisposeAsync();
await new PostgresContainer().DisposeAsync();

return;

async ValueTask<object?> RequireToken(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
{
    if (context.HttpContext.Request.Headers.Authorization.ToString() != $"Bearer {arguments.Token}")
        return Results.Unauthorized();

    return await next(context);
}
