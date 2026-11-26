using Cluster.Deploy;
using Common.Extensions;
using Microsoft.Extensions.Hosting;

namespace Cluster.Lobbies;

public static class OpenLobbiesWatcherExtensions
{
    // The open lobbies of the current deploy, followed on this host (the meta): the snapshot of the registry, then
    // every change it pushes.
    public static IHostApplicationBuilder AddOpenLobbiesWatcher(this IHostApplicationBuilder builder)
    {
        builder.Add<OpenLobbiesWatcher>()
               .As<IOpenLobbiesWatcher>()
               .As<IDeployAware>();

        return builder;
    }
}
