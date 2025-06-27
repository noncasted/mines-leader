using Common;
using ServiceLoop;

namespace Management.Configs;

public class ConfigsLoop : ISetupLoopStage
{
    public ConfigsLoop(IEnumerable<IConfig> configs)
    {
        _configs = configs;
    }

    private readonly IEnumerable<IConfig> _configs;

    public Task OnSetupStage(IReadOnlyLifetime lifetime)
    {
        return Task.WhenAll(_configs.Select(config => config.Refresh()));
    }
}