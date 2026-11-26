using Microsoft.Extensions.Hosting;

namespace Cluster.Monolith;

// The lifetime of a host of a monolith: it handles no signal. The ConsoleLifetime of every host would stop its host on
// SIGTERM by itself, and the runner could not tell a stop of the process from a host that stopped on its own; the
// runner owns SIGTERM / SIGINT / SIGQUIT and stops the hosts.
internal sealed class MonolithHostLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
