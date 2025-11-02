using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cluster.Discovery;

public interface IServiceDiscovery
{
    IServiceOverview Self { get; }
    IReadOnlyDictionary<Guid, IServiceOverview> Entries { get; }

    Task Start(IReadOnlyLifetime lifetime);
    Task Push();
}

public class ServiceDiscovery : IServiceDiscovery
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

    public Task Start(IReadOnlyLifetime lifetime)
    {
        UpdateLoop(lifetime).NoAwait();
        return _messaging.ListenQueue<IServiceOverview>(lifetime, _queueId, Update);
    }

    public Task Push()
    {
        try
        {
            _self = CreateOverview();
            return _messaging.PushDirectQueue(_queueId, _self);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Discovery] Pushing service overview failed");
        }

        return Task.CompletedTask;
    }

    private async Task UpdateLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            await Push();
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private void Update(IServiceOverview overview)
    {
        _entries[overview.Id] = overview;

        var toRemove = new List<Guid>();

        foreach (var (id, service) in _entries)
        {
            if (DateTime.UtcNow - service.UpdateTime > TimeSpan.FromSeconds(30))
                toRemove.Add(id);
        }

        foreach (var id in toRemove)
            _entries.Remove(id);
    }

    private IServiceOverview CreateOverview()
    {
        return _environment.Tag switch
        {
            ServiceTag.Game => new GameServerOverview
            {
                Id = _environment.ServiceId,
                Tag = ServiceTag.Game,
                UpdateTime = DateTime.UtcNow,
                Url = GetGameServerUrl(),
            },
            ServiceTag.Meta => new ServiceOverview()
            {
                Id = _environment.ServiceId,
                Tag = ServiceTag.Meta,
                UpdateTime = DateTime.UtcNow,
            },
            ServiceTag.Silo => new ServiceOverview()
            {
                Id = _environment.ServiceId,
                Tag = ServiceTag.Silo,
                UpdateTime = DateTime.UtcNow,
            },
            ServiceTag.Console => new ServiceOverview()
            {
                Id = _environment.ServiceId,
                Tag = ServiceTag.Console,
                UpdateTime = DateTime.UtcNow,
            },
            ServiceTag.Coordinator => new ServiceOverview()
            {
                Id = _environment.ServiceId,
                Tag = ServiceTag.Coordinator,
                UpdateTime = DateTime.UtcNow,
            },
            _ => throw new ArgumentOutOfRangeException()
        };

        string GetGameServerUrl()
        {
            var url = Environment.GetEnvironmentVariable("GAME_SERVER_URL");

            if (string.IsNullOrWhiteSpace(url))
                return "http://localhost:5268";

            return url;
        }
    }
}

public static class ServiceDiscoveryExtensions
{
    public static IHostApplicationBuilder AddServiceDiscovery(this IHostApplicationBuilder builder)
    {
        builder.Services.Add<ServiceDiscovery>()
            .As<IServiceDiscovery>();

        return builder;
    }

    public static GameServerOverview RandomServer(this IServiceDiscovery serviceDiscovery)
    {
        var servers = serviceDiscovery.Entries.Values
            .Where(t => t.Tag == ServiceTag.Game)
            .OfType<GameServerOverview>()
            .ToList();

        return servers.Random();
    }
}