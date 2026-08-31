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
    .AddMatchCommands()
    .AddAchievementCommands();

var app = builder.Build();

app.AddBackendMiddleware();

app.AddMonitorEndpoints();
app.MapDefaultEndpoints();
app.UseCors("cors");

app.Run();