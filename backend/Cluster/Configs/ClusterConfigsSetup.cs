using System.Text.Json;
using Common.Reactive;
using Infrastructure;
using Shared;

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
        if (_cardConfigs.Value.All.Count != 0)
            return;

        var configPath = Path.Combine(AppContext.BaseDirectory, "config.cards.json");

        if (!File.Exists(configPath))
            return;

        var json = await File.ReadAllTextAsync(configPath);
        var value = JsonSerializer.Deserialize<CardsConfigs>(json)!;
        await _cardConfigs.SetValue(value);
    }
}