using Aspire;
using Aspire.Hosting.ApplicationModel;
using Aspire.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Projects;
using Silo = Projects.Silo;

// Supports two modes:
//   dotnet Aspire.AppHost.dll              — dev orchestrator (aspire run)
//   dotnet Aspire.AppHost.dll --migrate    — production migrations only (migrator container)

if (args.Contains("--migrate"))
{
    Console.WriteLine("[Migrations] Starting idempotent database migrations");

    var migrationConfig = new ConfigurationManager();
    migrationConfig.AddEnvironmentVariables();

    var connectionFromCli = args.FirstOrDefault(a => a.StartsWith("--connection="))?.Substring("--connection=".Length);

    if (connectionFromCli != null)
    {
        migrationConfig.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:postgres"] = connectionFromCli,
            ["postgres"] = connectionFromCli,
        });
    }

    await PostResourcesSetup.Run(migrationConfig);

    Console.WriteLine("[Migrations] Done");
    return;
}

// --- Dev orchestrator mode (aspire run) ---

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
var meta = builder.AddProject<MetaGateway>("meta");

var console = builder
              .AddProject<ConsoleGateway>("console")
              .WithEnvironment("CONSOLE_TOKEN", consoleToken);

var game = builder
           .AddProject<GameGateway>("game")
           .WithEnvironment("GAME_SERVER_URL", serverUrl);

SetupDB();

coordinator.WaitFor(silo);
meta.WaitFor(silo).WaitFor(coordinator);
game.WaitFor(silo).WaitFor(coordinator);
console.WaitFor(silo).WaitFor(coordinator);

var migrationsCompleted = false;
builder.Eventing.Subscribe<BeforeResourceStartedEvent>(async (evt, _) =>
{
    if (evt.Resource.Name != "silo" || migrationsCompleted)
        return;

    migrationsCompleted = true;
    await PostResourcesSetup.Run(configuration);
});

builder.Build().Run();

return;

void SetupDB()
{
    var projectResources = new[]
    {
        silo,
        coordinator,
        meta,
        game,
        console
    };

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
