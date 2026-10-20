using Aspire;
using DeploySetup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchestration;
using Projects;
using Silo = Projects.Silo;

// AppHost is dev-only: production runs through docker-compose.yaml + Coolify, which
// does not boot this assembly.

var builder = DistributedApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

var configuration = builder.Configuration;
configuration.AddJsonFile("appsettings.local.json", true);

if (configuration.GetSection("Local").GetSection("KillPrevious").Get<bool>())
    ProcessCleanup.Run();

var serverUrl = configuration["LocalGameServerUrl"];
var consoleToken = configuration["ConsoleToken"] ?? "";

var upstream = DbUpstreamFactory.Create(builder, configuration);
var pgbouncer = PgBouncerFactory.Create(builder, upstream);

var dbConnection = $"Host=127.0.0.1;" +
                   $"Port={pgbouncer.Port};" +
                   $"Database={upstream.Database};" +
                   $"Username={upstream.User};" +
                   $"Password={upstream.Password}";

var silo = builder.AddProject<Silo>("silo");
silo.WaitFor(pgbouncer.Resource);

var coordinator = builder.AddProject<Coordinator>("coordinator");
var meta = builder
           .AddProject<MetaGateway>("meta")
           .WithEnvironment("CONSOLE_TOKEN", consoleToken);

var console = builder
              .AddProject<ConsoleGateway>("console")
              .WithEnvironment("CONSOLE_TOKEN", consoleToken);

var game = builder
           .AddProject<GameGateway>("game")
           .WithEnvironment("GAME_SERVER_URL", serverUrl)
           .WithEnvironment("CONSOLE_TOKEN", consoleToken);

SetupDB();
SetupOrleansIdentity();

coordinator.WaitFor(silo);
meta.WaitFor(silo).WaitFor(coordinator);
game.WaitFor(silo).WaitFor(coordinator);
console.WaitFor(silo).WaitFor(coordinator);

builder.Eventing.Subscribe<AfterResourcesCreatedEvent>((_, _) => PostResourcesSetup.Run(configuration));

builder.Build().Run();

return;

IResourceBuilder<ProjectResource>[] ProjectResources()
{
    return [silo, coordinator, meta, game, console];
}

// ClusterId / ServiceId are required by OrleansSetupExtensions. "dev" / "dev" are the values the local
// cluster used before they became explicit (UseLocalhostClustering defaults): keep them for the local cluster.
void SetupOrleansIdentity()
{
    var identity = OrleansSetupExtensions.ReadClusterIdentity(configuration);

    foreach (var resource in ProjectResources())
    {
        resource.WithEnvironment("Orleans__ClusterId", identity.ClusterId)
                .WithEnvironment("Orleans__ServiceId", identity.ServiceId);
    }
}

void SetupDB()
{
    var projectResources = ProjectResources();

    configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["postgres"] = dbConnection,
        ["ConnectionStrings__postgres"] = dbConnection
    });

    foreach (var resource in projectResources)
    {
        resource.WithEnvironment(context => {
            context.EnvironmentVariables["postgres"] = dbConnection;
            context.EnvironmentVariables["ConnectionStrings__postgres"] = dbConnection;
        });
    }
}
