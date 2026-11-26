using Cluster.Monolith;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[assembly: HostingStartup(typeof(MonolithHostingStartup))]

namespace Cluster.Monolith;

// Runs only in a host the runner names this assembly to (--hostingStartupAssemblies): the factory of a role stays the
// one of the separate service, and its host takes MonolithHostLifetime instead of the ConsoleLifetime of the defaults.
// The hosting startups run after the host defaults, so this registration wins.
internal sealed class MonolithHostingStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services => services.AddSingleton<IHostLifetime, MonolithHostLifetime>());
    }
}
