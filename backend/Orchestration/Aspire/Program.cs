using Aspire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Projects;
using Silo = Projects.Silo;

var builder = DistributedApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

var configuration = builder.Configuration;
configuration.AddJsonFile("appsettings.local.json", true);

if (configuration.GetSection("Local").GetSection("KillPrevious").Get<bool>())
    ProcessCleanup.Run();

var serverUrl = configuration["LocalGameServerUrl"];
var consoleToken = configuration["ConsoleToken"] ?? "";

var silo = builder.AddProject<Silo>("silo");

var coordinator = builder.AddProject<Coordinator>("coordinator");
var meta = builder.AddProject<MetaGateway>("meta");

var console = builder
              .AddProject<ConsoleGateway>("console")
              .WithEnvironment("CONSOLE_TOKEN", consoleToken);

var game = builder
           .AddProject<GameGateway>("game")
           .WithEnvironment("GAME_SERVER_URL", serverUrl);

coordinator.WaitFor(silo);
meta.WaitFor(silo).WaitFor(coordinator);
game.WaitFor(silo).WaitFor(coordinator);
console.WaitFor(silo).WaitFor(coordinator);

builder.Build().Run();