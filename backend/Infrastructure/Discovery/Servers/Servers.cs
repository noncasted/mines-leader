using Common;
using Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Discovery;

public interface IServers
{
    IReadOnlyDictionary<string, ServerOverview> Entries { get; }
}

public class Servers : BackgroundService, IServers
{
    public Servers(IMessagingClient messaging, IOptions<ServersOptions> options)
    {
        _messaging = messaging;
        _options = options;
    }

    private readonly IMessagingClient _messaging;
    private readonly IOptions<ServersOptions> _options;

    private readonly Dictionary<string, ServerOverview> _entries = new();

    public IReadOnlyDictionary<string, ServerOverview> Entries => _entries;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lifetime = stoppingToken.ToLifetime();
        Loop(stoppingToken.ToLifetime()).NoAwait();

        _messaging.Listen<ServerOverviewMessage>(lifetime, OnReceived);

        return Task.CompletedTask;

        void OnReceived(ServerOverviewMessage message)
        {
            var overview = message.Overview;
            
            // _logger.LogInformation("[Servers] Received server overview: {Name} {Url}",
            //     overview.Name,
            //     overview.Url);
            
            _entries[overview.Name] = overview;
        }
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            _entries.CleanupServers(_options.Value);
            await Task.Delay(_options.Value.CheckInterval, cancellationToken: lifetime.Token);
        }
    }
}