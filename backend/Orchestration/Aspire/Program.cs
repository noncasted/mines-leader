using Aspire;
using DeploySetup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orchestration;
using Projects;

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

// S4: one ticket key for meta and game. The development key unless appsettings sets "SessionTicketSecret".
var sessionTicketSecret = configuration["SessionTicketSecret"] ?? Convert.ToBase64String(SessionTicketsSetup.DevelopmentKey);

var upstream = DbUpstreamFactory.Create(builder, configuration);
var pgbouncer = PgBouncerFactory.Create(builder, upstream);
GrafanaFactory.Create(builder, configuration, upstream);

var dbConnection = $"Host=127.0.0.1;" +
                   $"Port={pgbouncer.Port};" +
                   $"Database={upstream.Database};" +
                   $"Username={upstream.User};" +
                   $"Password={upstream.Password}";

var silo = builder.AddProject<Silo_Service>("silo");
silo.WaitFor(pgbouncer.Resource);

var coordinator = builder.AddProject<Coordinator_Service>("coordinator");
var meta = builder
           .AddProject<MetaGateway_Service>("meta")
           .WithEnvironment("CONSOLE_TOKEN", consoleToken)
           .WithEnvironment(SessionTicketsSetup.SecretKey, sessionTicketSecret);

var console = builder
              .AddProject<ConsoleGateway_Service>("console")
              .WithEnvironment("CONSOLE_TOKEN", consoleToken);

SetupDashboardApi();

var game = builder
           .AddProject<GameGateway_Service>("game")
           .WithEnvironment("GAME_SERVER_URL", serverUrl)
           .WithEnvironment("CONSOLE_TOKEN", consoleToken)
           .WithEnvironment(SessionTicketsSetup.SecretKey, sessionTicketSecret);

SetupDB();
SetupOrleansIdentity();
SetupBenchmarks();

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

// Benchmarks are opt-in (BenchmarksAvailability): the dev cluster runs them against the local database.
// docker-compose.yaml (production) never sets the flag. The in-process migrator (PostResourcesSetup) reads it
// from this configuration to create the benchmark state tables.
void SetupBenchmarks()
{
    configuration[DeployStates.BenchmarksEnabledKey] = "true";

    foreach (var resource in ProjectResources())
        resource.WithEnvironment("Benchmarks__Enabled", "true");
}

// The console reads the backend traces of the client startup records from the telemetry API of the dashboard.
// The dashboard URL is the AppHost ASPNETCORE_URLS (launchSettings applicationUrl, the DCP proxy of the dashboard
// frontend). The API key is the one Aspire generates in CreateBuilder (AppHost:DashboardApiKey, kept in the user
// secrets of the AppHost) and passes to the dashboard as DASHBOARD__API__PRIMARYAPIKEY.
void SetupDashboardApi()
{
    var urls = configuration["ASPNETCORE_URLS"]
               ?? throw new InvalidOperationException("ASPNETCORE_URLS of the AppHost is not set: the dashboard URL is unknown");
    var dashboardUrl = urls.Split(';', StringSplitOptions.RemoveEmptyEntries)[0];

    var dashboardApiKey = configuration["AppHost:DashboardApiKey"]
                          ?? throw new InvalidOperationException("AppHost:DashboardApiKey is not set: the dashboard API is unsecured or disabled");

    console.WithEnvironment("AspireDashboard__Url", dashboardUrl)
           .WithEnvironment("AspireDashboard__ApiKey", dashboardApiKey);
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
