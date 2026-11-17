using Game.ClientTelemetry;
using Game.MatchMetrics;
using Meta.Matches;
using MetaGateway;
using MetaGateway.Matches;
using MetaGateway.Matchmaking;
using MetaGateway.UserFlow;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
using Orchestration;

var builder = WebApplication.CreateBuilder(args);

builder
    .SetupMetaGateway()
    .AddMatchmakingServices()
    .AddUserFlow()
    .AddUserCommands()
    .AddMatchCommands()
    .AddAchievementCommands()
    .AddClientTelemetryCommands()
    .AddMatchMetricsCommands();

builder.AddConsoleAccess();
builder.AddSessionTickets();
builder.AddMatchMetricsAdmission<MatchMetricsAdmission>();
builder.AddMatchMetricsReader();

var app = builder.Build();

app.AddBackendMiddleware();

app.AddMonitorEndpoints();
app.MapClientTelemetryRead().RequireConsoleToken();
app.MapMatchMetricsRead().RequireConsoleToken();
app.MapMatchHistoryRead().RequireConsoleToken();
app.MapDefaultEndpoints();

app.Run();