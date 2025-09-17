using Common;
using Infrastructure.Messaging;
using ServiceLoop;

namespace Services;

public interface IServiceDiscovery
{
    IReadOnlyDictionary<Guid, IServiceOverview> Entries { get; }
}

public class ServiceDiscovery : IServiceDiscovery, ISetupLoopStage
{
    public ServiceDiscovery(IMessaging messaging)
    {
        _messaging = messaging;
    }
    
    private readonly IMessaging _messaging;
    private readonly Dictionary<Guid, IServiceOverview> _entries = new();

    public IReadOnlyDictionary<Guid, IServiceOverview> Entries => _entries;

    public Task OnSetupStage(IReadOnlyLifetime lifetime)
    {
        _messaging.SendStream()
    }
}