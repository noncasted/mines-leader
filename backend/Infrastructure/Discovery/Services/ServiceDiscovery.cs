using Common;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using ServiceLoop;

namespace Services;

public interface IServiceDiscovery
{
    IReadOnlyDictionary<Guid, IServiceOverview> Entries { get; }
}

public class ServiceDiscovery : IServiceDiscovery, ISetupLoopStage
{
    public ServiceDiscovery(
        IMessaging messaging,
        IServiceEnvironment environment,
        ILogger<ServiceDiscovery> logger)
    {
        _messaging = messaging;
        _environment = environment;
        _logger = logger;
    }

    private readonly IMessaging _messaging;
    private readonly IServiceEnvironment _environment;
    private readonly ILogger<ServiceDiscovery> _logger;
    private readonly Dictionary<Guid, IServiceOverview> _entries = new();
    private readonly IMessageQueueId _queueId = new MessageQueueId("service-discovery");

    public IReadOnlyDictionary<Guid, IServiceOverview> Entries => _entries;

    public Task OnSetupStage(IReadOnlyLifetime lifetime)
    {
        _messaging.ListenQueue<IServiceOverview>(lifetime, _queueId, service => _entries[service.Id] = service);
        UpdateLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    private async Task UpdateLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            var overview = new ServiceOverview
            {
                Id = _environment.ServiceId,
                Name = _environment.ServiceName,
                Tag = _environment.Tag,
                UpdateTime = DateTime.UtcNow
            };

            try
            {
                await _messaging.PushDirectQueue(_queueId, overview);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Discovery] Pushing service overview failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}