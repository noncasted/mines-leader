using System.Text.Json;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Newtonsoft.Json;
using Shared;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Cluster.Configs;

public class ClusterConfigsSetup : ICoordinatorSetupCompleted
{
    public ClusterConfigsSetup(ICardConfigs cardConfigs)
    {
        _cardConfigs = cardConfigs;
    }

    private readonly ICardConfigs _cardConfigs;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        if (_cardConfigs.Value.BloodHound_Max != null)
            return;

        var configPath = Path.Combine(AppContext.BaseDirectory, "config.cards.json");

        if (!File.Exists(configPath))
            return;
        
        var json = await File.ReadAllTextAsync(configPath);
        var value = JsonUtils.Deserialize<CardsConfigs>(json)!;
        await _cardConfigs.SetValue(value);
    }
}