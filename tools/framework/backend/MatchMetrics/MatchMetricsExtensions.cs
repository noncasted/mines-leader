using Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Game.MatchMetrics;

// The tables come from the deploy setup of the game (MatchMetricsSchema.Create). Every host has IDbSource.
public static class MatchMetricsExtensions
{
    // The gateway of the clients: the streams of its sockets. The game adds its admission (AddMatchMetricsAdmission).
    public static IHostApplicationBuilder AddMatchMetrics(this IHostApplicationBuilder builder, MatchMetricsOptions? options = null)
    {
        builder.AddMatchMetricsStorage(options);
        builder.Services.AddSingleton<IMatchMetricsConnections, MatchMetricsConnections>();

        return builder;
    }

    // A host that writes the stream of the server: the game server.
    public static IHostApplicationBuilder AddMatchMetricsWriter(this IHostApplicationBuilder builder, MatchMetricsOptions? options = null)
    {
        builder.AddMatchMetricsStorage(options);
        builder.Services.AddSingleton<IMatchMetricsWriter, MatchMetricsWriter>();

        return builder;
    }

    // The reading for the console, the agents and the history of matches (IMatchMetricsReader), the endpoints are
    // MapMatchMetricsRead().RequireConsoleToken().
    public static IHostApplicationBuilder AddMatchMetricsReader(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<IMatchMetricsReader, MatchMetricsReader>();

        return builder;
    }

    // Who may write into a match: the answer of the game.
    public static IHostApplicationBuilder AddMatchMetricsAdmission<T>(this IHostApplicationBuilder builder) where T : class, IMatchMetricsAdmission
    {
        builder.Services.AddSingleton<IMatchMetricsAdmission, T>();

        return builder;
    }

    private static void AddMatchMetricsStorage(this IHostApplicationBuilder builder, MatchMetricsOptions? options)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton(options ?? new MatchMetricsOptions());
        builder.Services.TryAddSingleton<MatchMetricSeriesDictionary>();
        builder.Services.TryAddSingleton<MatchMetricStreamStore>();
        builder.Services.TryAddSingleton<MatchMetricBatchWriter>();
    }
}
