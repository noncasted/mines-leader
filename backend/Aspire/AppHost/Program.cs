using Microsoft.Extensions.Configuration;
using Projects;
using Console = Projects.Console;

var builder = DistributedApplication.CreateBuilder(args);

var startup = builder.AddProject<Startup>("startup");
var silo = builder.AddProject<Silo>("silo");
var coordinator = builder.AddProject<Coordinator>("coordinator");
var backend = builder.AddProject<BackendGateway>("backend");

var game = builder.AddProject<GameGateway>("game")
    .WithEnvironment(options =>
        options.EnvironmentVariables["GAME_SERVER_URL"] = Environment.GetEnvironmentVariable("GAME_SERVER_URL")
    );

var console = builder.AddProject<Console>("console");

SetupDB();

silo.WaitForCompletion(startup);
coordinator.WaitFor(silo);
backend.WaitFor(silo);
game.WaitFor(silo);
console.WaitFor(silo);

SetDashboardToken();

builder.Build().Run();

return;

void SetupDB()
{
    var externalDb = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

    var projectResources = new[]
    {
        startup,
        silo,
        coordinator,
        backend,
        game,
        console
    };

    if (externalDb == null)
        externalDb = builder.Configuration.GetConnectionString("db");

    foreach (var resource in projectResources)
        resource.WithEnvironment(context => context.EnvironmentVariables["ConnectionStrings__postgres"] = externalDb);
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