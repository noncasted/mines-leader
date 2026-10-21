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

builder.AddConsoleAccess();
builder.AddSessionTickets();

var app = builder.Build();

app.AddBackendMiddleware();

app.AddMonitorEndpoints();
app.MapDefaultEndpoints();

app.Run();