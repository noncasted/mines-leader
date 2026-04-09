using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<Guid, IServiceOverview> _entries = new();
    private readonly IRuntimeChannelId _channelId = new RuntimeChannelId("service-discovery");

    private volatile IServiceOverview _self;

    public IServiceOverview Self => _self;
    public IReadOnlyDictionary<Guid, IServiceOverview> Entries => _entries;

    public async Task Start(IReadOnlyLifetime lifetime)
    {
        try
        {
            UpdateLoop(lifetime).NoAwait();
            await _messaging.ListenChannel<IServiceOverview>(lifetime, _channelId, Update);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ServiceDiscovery] Start failed");
        }
    }

    public async Task Push()
    {
        try
        {
            _self = CreateOverview();
            await _messaging.PublishChannel(_channelId, _self);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ServiceDiscovery] Push failed");
        }
    }

    private async Task UpdateLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            try
            {
                await Push();
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[ServiceDiscovery] UpdateLoop iteration failed");
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }

    private void Update(IServiceOverview overview)
    {
        try
        {
            _entries[overview.Id] = overview;

            foreach (var (id, service) in _entries)
            {
                if (DateTime.UtcNow - service.UpdateTime > TimeSpan.FromSeconds(30))
                {
                    if (_entries.TryRemove(id, out _))
                        _logger.LogInformation("[ServiceDiscovery] Removed stale entry {Id} (tag={Tag})", id,
                            service.Tag
                        );
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ServiceDiscovery] Update failed");
        }
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
            ServiceTag.Meta => new ServiceOverview
            {
                Id = _environment.ServiceId,
                Tag = ServiceTag.Meta,
                UpdateTime = DateTime.UtcNow,
            },
            ServiceTag.Silo => new ServiceOverview
            {
                Id = _environment.ServiceId,
                Tag = ServiceTag.Silo,
                UpdateTime = DateTime.UtcNow,
            },
            ServiceTag.Console => new ServiceOverview
            {
                Id = _environment.ServiceId,
                Tag = ServiceTag.Console,
                UpdateTime = DateTime.UtcNow,
            },
            ServiceTag.Coordinator => new ServiceOverview
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
        builder.Add<ServiceDiscovery>()
            .As<IServiceDiscovery>();

        return builder;
    }

    public static GameServerOverview RandomServer(this IServiceDiscovery serviceDiscovery)
    {
        var servers = serviceDiscovery.Entries.Values
            .Where(t => t.Tag == ServiceTag.Game)
            .OfType<GameServerOverview>()
            .ToList();

        if (servers.Count == 0)
            throw new InvalidOperationException("No game servers available");

        return servers.Random();
    }
}