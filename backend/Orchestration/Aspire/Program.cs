using Aspire;
using Microsoft.Extensions.Configuration;
using Projects;
using Silo = Projects.Silo;

Console.WriteLine("[AppHost] Environment variables:");
foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
    Console.WriteLine($"[AppHost]   {entry.Key}={entry.Value}");

var builder = DistributedApplication.CreateBuilder(args);

var configuration = builder.Configuration;
configuration.AddJsonFile("appsettings.local.json", true);

var dbConnection = await GetOrCreateDb();

var silo = builder.AddProject<Silo>("silo");
var coordinator = builder.AddProject<Coordinator>("coordinator");
var backend = builder.AddProject<MetaGateway>("backend");
var console = builder.AddProject<ConsoleGateway>("console");

var game = builder.AddProject<GameGateway>("game")
    .WithEnvironment(options =>
        options.EnvironmentVariables["GAME_SERVER_URL"] = Environment.GetEnvironmentVariable("GAME_SERVER_URL")!
    );

SetupDB();

coordinator.WaitFor(silo);
backend.WaitFor(silo);
game.WaitFor(silo);
console.WaitFor(silo);

SetDashboardToken();

builder.Eventing.Subscribe<AfterResourcesCreatedEvent>(async (_, _) =>
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                var localSection = builder.Configuration.GetSection("Local");
                var requiresDrop = localSection.GetSection("DropStates").Get<bool>();
                var requiresCleanup = localSection.GetSection("ClearStates").Get<bool>();

                if (requiresDrop == true)
                    await StatesDrop.Run(configuration);

                await OrleansSetup.Run(configuration);
                await StatesSetup.Run(configuration);
                await SideEffectsSetup.Run(configuration);

                if (requiresCleanup == true)
                    await StatesCleanup.Run(configuration);

                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Aspire] Setup attempt {attempt}/5 failed: {ex.Message}");

                if (attempt < 5)
                    await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        Console.WriteLine("[Aspire] Setup failed after 5 attempts");
    }
);

builder.Build().Run();

return;

void SetupDB()
{
    var projectResources = new[]
    {
        silo,
        coordinator,
        backend,
        game,
        console
    };

    foreach (var resource in projectResources)
        resource.WithEnvironment(context => context.EnvironmentVariables["ConnectionStrings__postgres"] = dbConnection);
}

void SetDashboardToken()
{
    var token = Environment.GetEnvironmentVariable("ASPIRE_TOKEN");

    if (token == null)
        return;

    configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppHost:BrowserToken"] = token,
        }
    );
}

Task<string> GetOrCreateDb()
{
    var externalDb = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

    if (externalDb != null)
        return Task.FromResult(externalDb);

    var localDb = configuration.GetConnectionString("db")!;

    var parts = localDb
        .Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Split('=', 2))
        .Where(p => p.Length == 2)
        .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

    var host = parts["Server"];
    var port = int.Parse(parts["Port"]);
    var database = parts["Database"];
    var user = parts["User Id"];
    var password = parts["Password"];

    builder
        .AddContainer("postgres", "postgres", "17.6")
        .WithHttpEndpoint(port: port, targetPort: 5432, name: "tcp", isProxied: false)
        .WithVolume("mines-leader-postgres-data", "/var/lib/postgresql/data")
        .WithEnvironment("POSTGRES_PASSWORD", password)
        .WithEnvironment("POSTGRES_DB", database)
        .WithEnvironment("POSTGRES_USER", user)
        .WithEnvironment("POSTGRES_HOST_AUTH_METHOD", "trust")
        .WithLifetime(ContainerLifetime.Persistent);

    return Task.FromResult($"Host={host};Port={port};Database={database};Username={user};Password={password}");
}