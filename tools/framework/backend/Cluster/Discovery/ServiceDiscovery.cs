using System.Collections.Concurrent;
using Cluster.Deploy;
using Common.Extensions;
using Internal;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cluster.Discovery;

public interface IServiceDiscovery
{
    IServiceOverview Self { get; }
    IReadOnlyDictionary<Guid, IServiceOverview> Entries { get; }

    Task Start(IReadOnlyLifetime lifetime);
    Task Push();
    Task Unregister();
}

public class ServiceDiscovery : IServiceDiscovery
{
    public ServiceDiscovery(
        IOrleans orleans,
        IDeployContext deployContext,
        IServiceEnvironment environment,
        IOptions<ServiceDiscoveryOptions> options,
        ILogger<ServiceDiscovery> logger)
    {
        _orleans = orleans;
        _deployContext = deployContext;
        _environment = environment;
        _publishGameServerUrl = options.Value.PublishGameServerUrl;
        _logger = logger;

        _self = CreateOverview();
    }

    private readonly IOrleans _orleans;
    private readonly IDeployContext _deployContext;
    private readonly IServiceEnvironment _environment;
    private readonly bool _publishGameServerUrl;
    private readonly ILogger<ServiceDiscovery> _logger;
    private readonly ConcurrentDictionary<Guid, IServiceOverview> _entries = new();

    private volatile IServiceOverview _self;

    // Set by Unregister (ApplicationStopping): the refresh loop runs on until the host stops it and must not
    // register the stopping service again.
    private volatile bool _isUnregistered;

    public IServiceOverview Self => _self;
    public IReadOnlyDictionary<Guid, IServiceOverview> Entries => _entries;

    public Task Start(IReadOnlyLifetime lifetime)
    {
        RefreshLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    // A failed push throws: the callers react (the refresh loop retries, the startup and the health check log).
    public async Task Push()
    {
        _self = CreateOverview();

        if (_deployContext.DeployId == Guid.Empty || _isUnregistered == true)
            return;

        var grain = _orleans.GetGrain<IServiceDiscoveryStorage>(_deployContext.DeployId);
        var members = await grain.Update(_self);

        ApplyMembers(members);
    }

    public async Task Unregister()
    {
        _isUnregistered = true;

        try
        {
            if (_deployContext.DeployId == Guid.Empty)
                return;

            var grain = _orleans.GetGrain<IServiceDiscoveryStorage>(_deployContext.DeployId);
            await grain.Unregister(_environment.ServiceId);
            _logger.LogInformation("[ServiceDiscovery] Unregistered service {ServiceId}", _environment.ServiceId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ServiceDiscovery] Unregister failed");
        }
    }

    private async Task RefreshLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            var delay = TimeSpan.FromSeconds(2);

            try
            {
                await Push();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[ServiceDiscovery] Push failed, retrying in 5 s");
                delay = TimeSpan.FromSeconds(5);
            }

            try
            {
                await Task.Delay(delay, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await Unregister();
    }

    private void ApplyMembers(Dictionary<Guid, IServiceOverview> members)
    {
        foreach (var (id, overview) in members)
            _entries[id] = overview;

        foreach (var existingId in _entries.Keys.ToList())
        {
            if (members.ContainsKey(existingId) == false)
                _entries.TryRemove(existingId, out _);
        }
    }

    private IServiceOverview CreateOverview()
    {
        // Game servers publish their public url (matchmaking hands it to clients); other roles only their presence.
        if (_publishGameServerUrl == true)
        {
            return new GameServerOverview
            {
                Id = _environment.ServiceId,
                Tag = _environment.Tag,
                UpdateTime = DateTime.UtcNow,
                Url = GetGameServerUrl(),
            };
        }

        return new ServiceOverview
        {
            Id = _environment.ServiceId,
            Tag = _environment.Tag,
            UpdateTime = DateTime.UtcNow,
        };

        string GetGameServerUrl()
        {
            var url = Environment.GetEnvironmentVariable("GAME_SERVER_URL");

            if (url == null)
                throw new InvalidOperationException("GAME_SERVER_URL environment variable is not set");

            return url;
        }
    }
}
