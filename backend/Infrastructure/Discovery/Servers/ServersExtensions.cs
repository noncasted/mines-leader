using Common;
using Microsoft.Extensions.Hosting;
using ServiceLoop;

namespace Infrastructure.Discovery;

public static class ServersExtensions
{
    public static void CleanupServers(this Dictionary<string, ServerOverview> overviews, ServersOptions options)
    {
        var toRemove = new List<string>();

        foreach (var (name, overview) in overviews)
        {
            var difference = DateTime.UtcNow - overview.UpdateTime;

            if (difference > options.TimeOut)
                toRemove.Add(name);
        }

        foreach (var name in toRemove)
            overviews.Remove(name);
    }

    public static IHostApplicationBuilder AddServersCollection(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHostedSingleton<IServers, Servers>();
        builder.AddEnvironmentOptions<ServersOptions>("appsettings.servers");

        return builder;
    }

    public static IHostApplicationBuilder AddServerOverviewPusher(this IHostApplicationBuilder builder)
    {
        builder.Services.Add<ServerOverviewPusher>()
            .AsMessagingLoopStage();
        
        builder.AddEnvironmentOptions<ServersOptions>("appsettings.servers");

        return builder;
    }
}