using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Shared;

namespace Cluster.Configs;

public class ClusterConfigsSetup : ICoordinatorSetupCompleted
{
    public ClusterConfigsSetup(ICardConfigs cardConfig, IBotConfig botConfig)
    {
        _cardConfig = cardConfig;
        _botConfig = botConfig;
    }

    private readonly ICardConfigs _cardConfig;
    private readonly IBotConfig _botConfig;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        if (_cardConfig.IsInitialized == false)
        {
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "config.cards.json");
                var json = await File.ReadAllTextAsync(configPath);
                var value = JsonUtils.Deserialize<CardConfigOptions>(json)!;
                await _cardConfig.SetValue(value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load card config: {ex.Message}");
            }
        }

        if (_botConfig.IsInitialized == false)
        {
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "config.bot.json");
                var json = await File.ReadAllTextAsync(configPath);
                var value = JsonUtils.Deserialize<BotConfigOptions>(json)!;
                await _botConfig.SetValue(value);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load bot config: {e.Message}");
            }
        }
    }
}