using MetaGateway;
using MetaGateway.Matchmaking;
using MetaGateway.UserFlow;
using MetaGateway.UserFlow.Connection;
using Orchestration;

var builder = WebApplication.CreateBuilder(args);

builder
    .SetupMetaGateway()
    .AddMatchmakingServices()
    .AddUserFlow()
    .AddUserCommands()
    .AddLootCommands();

var app = builder.Build();

app.AddIdentityEndpoints();
app.AddBackendMiddleware();

app.AddMonitorEndpoints();
app.MapDefaultEndpoints();
app.UseCors("cors");

app.Run();