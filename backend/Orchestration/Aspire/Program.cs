using Aspire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Projects;
using Silo = Projects.Silo;

var builder = DistributedApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

Console.WriteLine("[Aspire] ===== AppHost starting =====");

Console.WriteLine(
    $"[Aspire] SSL_CERT_DIR          = {Environment.GetEnvironmentVariable("SSL_CERT_DIR") ?? "(not set)"}");

Console.WriteLine(
    $"[Aspire] Kestrel cert path      = {Environment.GetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Path") ?? "(not set)"}");

Console.WriteLine(
    $"[Aspire] Kestrel cert password  = {(Environment.GetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Password") != null ? "***" : "(not set)")}");

Console.WriteLine(
    $"[Aspire] ASPIRE_TOKEN           = {(Environment.GetEnvironmentVariable("ASPIRE_TOKEN") != null ? "***" : "(not set)")}");

Console.WriteLine(
    $"[Aspire] GAME_SERVER_URL        = {Environment.GetEnvironmentVariable("GAME_SERVER_URL") ?? "(not set)"}");

Console.WriteLine(
    $"[Aspire] DB_CONNECTION_STRING   = {(Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") != null ? "***" : "(not set)")}");

var configuration = builder.Configuration;
configuration.AddJsonFile("appsettings.local.json", true);

Console.WriteLine("[Aspire] Configuration loaded");

if (configuration.GetSection("Local").GetSection("KillPrevious").Get<bool>())
{
    Console.WriteLine("[Aspire] KillPrevious=true, running ProcessCleanup...");
    ProcessCleanup.Run();
    Console.WriteLine("[Aspire] ProcessCleanup done");
}

Console.WriteLine("[Aspire] Setting up database...");
var dbConnection = await GetOrCreateDb();
Console.WriteLine("[Aspire] Database ready");

Console.WriteLine("[Aspire] Registering projects...");
var silo = builder.AddProject<Silo>("silo");
var coordinator = builder.AddProject<Coordinator>("coordinator");
var meta = builder.AddProject<MetaGateway>("meta");
var consoleToken = Environment.GetEnvironmentVariable("ASPIRE_TOKEN") ?? configuration["ConsoleToken"] ?? "";

var console = builder.AddProject<ConsoleGateway>("console")
                     .WithEnvironment("CONSOLE_TOKEN", consoleToken);

var game = builder.AddProject<GameGateway>("game")
                  .WithEnvironment(options =>
                      options.EnvironmentVariables["GAME_SERVER_URL"] =
                          Environment.GetEnvironmentVariable("GAME_SERVER_URL")!);

Console.WriteLine("[Aspire] Projects registered: silo, coordinator, meta, console, game");

SetupDB();
Console.WriteLine("[Aspire] DB environment injected into all projects");

coordinator.WaitFor(silo);
meta.WaitFor(silo);
game.WaitFor(silo);
console.WaitFor(silo);

Console.WriteLine("[Aspire] WaitFor(silo) set for: coordinator, meta, game, console");

SetDashboardToken();

builder.Eventing.Subscribe<AfterResourcesCreatedEvent>(async (_, _) => {
    Console.WriteLine("[Aspire] AfterResourcesCreated — starting post-setup...");

    for (var attempt = 1; attempt <= 5; attempt++)
    {
        try
        {
            var localSection = builder.Configuration.GetSection("Local");
            var requiresDrop = localSection.GetSection("DropStates").Get<bool>();
            var requiresCleanup = localSection.GetSection("ClearStates").Get<bool>();

            Console.WriteLine(
                $"[Aspire] Setup attempt {attempt}/5 (DropStates={requiresDrop}, ClearStates={requiresCleanup})");

            if (requiresDrop == true)
            {
                Console.WriteLine("[Aspire] Running StatesDrop...");
                await StatesDrop.Run(configuration);
                Console.WriteLine("[Aspire] StatesDrop done");
            }

            Console.WriteLine("[Aspire] Running StatesSetup...");
            await StatesSetup.Run(configuration);
            Console.WriteLine("[Aspire] StatesSetup done");

            Console.WriteLine("[Aspire] Running SideEffectsSetup...");
            await SideEffectsSetup.Run(configuration);
            Console.WriteLine("[Aspire] SideEffectsSetup done");

            Console.WriteLine("[Aspire] Running BenchmarkSetup...");
            await BenchmarkSetup.Run(configuration);
            Console.WriteLine("[Aspire] BenchmarkSetup done");

            Console.WriteLine("[Aspire] Running AuditLogSetup...");
            await AuditLogSetup.Run(configuration);
            Console.WriteLine("[Aspire] AuditLogSetup done");

            if (requiresCleanup == true)
            {
                Console.WriteLine("[Aspire] Running StatesCleanup...");
                await StatesCleanup.Run(configuration);
                Console.WriteLine("[Aspire] StatesCleanup done");
            }

            Console.WriteLine("[Aspire] Post-setup completed successfully");
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

void SetDashboardToken()
{
    var token = Environment.GetEnvironmentVariable("ASPIRE_TOKEN");

    if (token == null)
    {
        Console.WriteLine("[Aspire] ASPIRE_TOKEN not set, dashboard token skipped");
        return;
    }

    configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["AppHost:BrowserToken"] = token,
    });

    Console.WriteLine("[Aspire] Dashboard token configured");
}

Task<string> GetOrCreateDb()
{
    var externalDb = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

    if (externalDb != null)
    {
        Console.WriteLine($"[AppHost] [DB] Received external db: {externalDb}");
        return Task.FromResult(externalDb);
    }

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

    var postgres = builder
                   .AddContainer("postgres", "postgres", "17.6")
                   .WithHttpEndpoint(port: port, targetPort: 5432, name: "tcp", isProxied: false)
                   .WithVolume("mines-leader-postgres-data", "/var/lib/postgresql/data")
                   .WithEnvironment("POSTGRES_PASSWORD", password)
                   .WithEnvironment("POSTGRES_DB", database)
                   .WithEnvironment("POSTGRES_USER", user)
                   .WithEnvironment("POSTGRES_HOST_AUTH_METHOD", "trust")
                   .WithLifetime(ContainerLifetime.Persistent);

    var pgbouncerPort = port + 1;
    var pgbouncerConfigPath = Path.Combine(builder.AppHostDirectory, "ContainersData/PgBouncer/pgbouncer.ini");
    var pgbouncerUserlistPath = Path.Combine(builder.AppHostDirectory, "ContainersData/PgBouncer/userlist.txt");

    builder.AddContainer("pgbouncer", "edoburu/pgbouncer", "latest")
           .WithHttpEndpoint(port: pgbouncerPort, targetPort: 6432, name: "pgbouncer-port", isProxied: false)
           .WithBindMount(pgbouncerConfigPath, "/etc/pgbouncer/pgbouncer.ini", isReadOnly: true)
           .WithBindMount(pgbouncerUserlistPath, "/etc/pgbouncer/userlist.txt", isReadOnly: true)
           .WaitFor(postgres)
           .WithLifetime(ContainerLifetime.Persistent);

    var result = $"Host={host};Port={pgbouncerPort};Database={database};Username={user};Password={password}";
    Console.WriteLine($"[AppHost] [DB] Create db string from options: {result} (via PgBouncer:{pgbouncerPort})");
    return Task.FromResult(result);
}