using Aspire;
using Microsoft.Extensions.Configuration;
using Projects;
using Console = Projects.Console;
using Silo = Projects.Silo;

var builder = DistributedApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.local.json", true);

var dbConnection = GetDbConnectionString();
await builder.SetupDb(dbConnection);

var silo = builder.AddProject<Silo>("silo");
var coordinator = builder.AddProject<Coordinator>("coordinator");
var backend = builder.AddProject<MetaGateway>("backend");
var console = builder.AddProject<Console>("console");

var game = builder.AddProject<GameGateway>("game")
    .WithEnvironment(options =>
        options.EnvironmentVariables["GAME_SERVER_URL"] = Environment.GetEnvironmentVariable("GAME_SERVER_URL")
    );

SetupDB();

coordinator.WaitFor(silo);
backend.WaitFor(silo);
game.WaitFor(silo);
console.WaitFor(silo);

SetDashboardToken();

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

    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppHost:BrowserToken"] = token,
        }
    );
}

string GetDbConnectionString()
{
    var externalDb = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

    if (externalDb != null)
        return externalDb;

    return builder.Configuration.GetConnectionString("db") ??
           throw new Exception("Database connection string is not set");
}