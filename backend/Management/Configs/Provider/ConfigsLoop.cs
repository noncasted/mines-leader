using Common;
using ServiceLoop;

namespace Management.Configs;

public class ConfigsLoop : ILocalSetupCompleted
{
    public ConfigsLoop(IEnumerable<IConfig> configs)
    {
        _configs = configs;
    }

    private readonly IEnumerable<IConfig> _configs;

    public Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        return Task.WhenAll(_configs.Select(config => config.Refresh()));
    }
}