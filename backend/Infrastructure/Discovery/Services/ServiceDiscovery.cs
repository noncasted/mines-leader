using Common;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceLoop;

namespace Services;

public interface IServiceDiscovery
{
    IServiceOverview Self { get; }
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

        _self = CreateOverview();
    }

    private readonly IMessaging _messaging;
    private readonly IServiceEnvironment _environment;
    private readonly ILogger<ServiceDiscovery> _logger;
    private readonly Dictionary<Guid, IServiceOverview> _entries = new();
    private readonly IMessageQueueId _queueId = new MessageQueueId("service-discovery");

    private IServiceOverview _self;

    public IServiceOverview Self => _self;
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
            _self = CreateOverview();

            try
            {
                await _messaging.PushDirectQueue(_queueId, _self);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Discovery] Pushing service overview failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private IServiceOverview CreateOverview()
    {
        return _environment.Tag switch
        {
            ServiceTag.Server => new GameServerOverview
            {
                Id = _environment.ServiceId,
                Name = _environment.ServiceName,
                Tag = ServiceTag.Server,
                UpdateTime = DateTime.UtcNow,
                Url = _environment.ServiceUrl
            },
            ServiceTag.Backend => new ServiceOverview()
            {
                Id = _environment.ServiceId,
                Name = _environment.ServiceName,
                Tag = ServiceTag.Backend,
                UpdateTime = DateTime.UtcNow,
            },
            ServiceTag.Silo => new ServiceOverview()
            {
                Id = _environment.ServiceId,
                Name = _environment.ServiceName,
                Tag = ServiceTag.Silo,
                UpdateTime = DateTime.UtcNow,
            },
            ServiceTag.Console => new ServiceOverview()
            {
                Id = _environment.ServiceId,
                Name = _environment.ServiceName,
                Tag = ServiceTag.Console,
                UpdateTime = DateTime.UtcNow,
            },
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public static class ServiceDiscoveryExtensions
{
    public static IHostApplicationBuilder AddServiceDiscovery(this IHostApplicationBuilder builder)
    {
        builder.Services.Add<ServiceDiscovery>()
            .As<IServiceDiscovery>()
            .AsSetupLoopStage();

        return builder;
    }

    public static GameServerOverview RandomServer(this IServiceDiscovery serviceDiscovery)
    {
        var servers = serviceDiscovery.Entries.Values
            .Where(t => t.Tag == ServiceTag.Server)
            .OfType<GameServerOverview>()
            .ToList();

        return servers.Random();
    }
}