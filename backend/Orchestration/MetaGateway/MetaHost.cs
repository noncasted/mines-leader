using Game.ClientTelemetry;
using Game.MatchMetrics;
using Meta.Matches;
using MetaGateway.Matches;
using MetaGateway.Matchmaking;
using MetaGateway.UserFlow;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
using Orchestration;

namespace MetaGateway;

// The host of the meta gateway role: the separate service (Services/MetaGateway) runs it alone, a monolith next to
// the other roles.
public static class MetaHost
{
    public static WebApplication Build(WebApplicationOptions options)
    {
        var builder = WebApplication.CreateBuilder(options);

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

        return app;
    }
}
