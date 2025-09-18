using Common;
using Infrastructure.Messaging;
using ServiceLoop;

namespace Management.Configs;

public class ConfigsObserver : ISetupLoopStage
{
    public ConfigsObserver(
        IEnumerable<IConfig> config,
        IMessaging messaging)
    {
        _config = config.ToDictionary(t => t.Type);
        _messaging = messaging;
    }

    private readonly IReadOnlyDictionary<Type, IConfig> _config;
    private readonly IMessaging _messaging;
    
    public Task OnSetupStage(IReadOnlyLifetime lifetime)
    {
        
        return Task.CompletedTask;
    }

    private Task OnUpdated(ConfigUpdateMessage message)
    {
        var value = message.Value;
        _config[value.GetType()].OnReceived(value);
        return Task.CompletedTask;
    }
}