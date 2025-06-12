using Common;
using Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Discovery;

public class ServerOverviewPusher : BackgroundService
{
    public ServerOverviewPusher(
        IServiceEnvironment environment,
        IMessagingClient messaging,
        ILogger<ServerOverviewPusher> logger,
        IOptions<ServersOptions> options)
    {
        _environment = environment;
        _messaging = messaging;
        _logger = logger;
        _options = options;
    }

    private readonly IServiceEnvironment _environment;
    private readonly IMessagingClient _messaging;
    private readonly ILogger<ServerOverviewPusher> _logger;
    private readonly IOptions<ServersOptions> _options;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Loop(stoppingToken.ToLifetime()).NoAwait();
        return Task.CompletedTask;
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            var overview = new ServerOverview
            {
                Name = _environment.ServiceName,
                Url = _environment.ServiceUrl,
                UpdateTime = DateTime.UtcNow,
                ClientId = _environment.ServiceId
            };

            try
            {
                // _logger.LogInformation("[Servers] Pushing server overview: {Name} {Url}", overview.Name, overview.Url);

                await _messaging.Send(ServiceTag.Backend, new ServerOverviewMessage
                {
                    Overview = overview
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to push server overview");
            }

            await Task.Delay(_options.Value.UpdateInterval, cancellationToken: lifetime.Token);
        }
    }
}