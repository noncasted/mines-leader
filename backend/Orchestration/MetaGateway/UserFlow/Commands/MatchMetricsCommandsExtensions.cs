using Game.MatchMetrics;

namespace MetaGateway.UserFlow.Commands;

public static class MatchMetricsCommandsExtensions
{
    // The admission of the game (IMatchMetricsAdmission over the record of the match) is registered by the meta.
    public static IHostApplicationBuilder AddMatchMetricsCommands(this IHostApplicationBuilder builder)
    {
        builder.AddMatchMetrics();
        builder.AddUserCommand<MatchMetricsCommands.Open>();
        builder.AddUserCommand<MatchMetricsCommands.Batch>();

        return builder;
    }
}
