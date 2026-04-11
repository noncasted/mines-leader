using Microsoft.Extensions.DependencyInjection;

namespace Game.GamePlay;

public static class BotServiceExtensions
{
    public static IServiceCollection AddBotServices(this IServiceCollection services)
    {
        services.AddSingleton<IBotRunner, BotRunner>();
        services.AddSingleton<IBotContext, BotContext>();
        services.AddSingleton<IBotCardStrategies, BotCardStrategies>();

        services.AddSingleton<BotBoardUtils>();
        services.AddSingleton<IBotCommandUtils, BotCommandUtils>();

        services.AddSingleton<IBotCardAction, BotCardAction>();
        services.AddSingleton<IBotCellAction, BotCellAction>();
        services.AddSingleton<IBotFlagAction, BotFlagAction>();

        services.AddSingleton<IBotCardStrategy, BloodhoundStrategy>();
        services.AddSingleton<IBotCardStrategy, TrebuchetStrategy>();
        services.AddSingleton<IBotCardStrategy, TrebuchetAimerStrategy>();
        services.AddSingleton<IBotCardStrategy, ErosionDozerStrategy>();
        services.AddSingleton<IBotCardStrategy, GravediggerStrategy>();
        services.AddSingleton<IBotCardStrategy, ZipZapStrategy>();
        services.AddSingleton<IBotCardStrategy, OpponentFlagEraseStrategy>();
        services.AddSingleton<IBotCardStrategy, OpponentFlagReshuffleStrategy>();
        services.AddSingleton<IBotCardStrategy, OpponentBombStrategy>();
        services.AddSingleton<IBotCardStrategy, SmokeStrategy>();

        services.AddSingleton<IBotCardStrategy, MedicStrategy>();
        services.AddSingleton<IBotCardStrategy, MinefieldScoutStrategy>();
        services.AddSingleton<IBotCardStrategy, SiphonStrategy>();
        services.AddSingleton<IBotCardStrategy, ChainReactionStrategy>();
        services.AddSingleton<IBotCardStrategy, OverclockStrategy>();
        services.AddSingleton<IBotCardStrategy, FogOfWarStrategy>();
        services.AddSingleton<IBotCardStrategy, ScavengerStrategy>();
        services.AddSingleton<IBotCardStrategy, HandScrambleStrategy>();
        services.AddSingleton<IBotCardStrategy, LockdownStrategy>();
        services.AddSingleton<IBotCardStrategy, SonarStrategy>();
        services.AddSingleton<IBotCardStrategy, PurgeStrategy>();
        services.AddSingleton<IBotCardStrategy, AdrenalineStrategy>();
        services.AddSingleton<IBotCardStrategy, ManaSurgeStrategy>();
        services.AddSingleton<IBotCardStrategy, BloodPactStrategy>();
        services.AddSingleton<IBotCardStrategy, CoinTossStrategy>();
        services.AddSingleton<IBotCardStrategy, ManaFountainStrategy>();
        services.AddSingleton<IBotCardStrategy, FocusStrategy>();
        services.AddSingleton<IBotCardStrategy, ShieldStrategy>();
        services.AddSingleton<IBotCardStrategy, PowerSurgeStrategy>();
        services.AddSingleton<IBotCardStrategy, EmbargoStrategy>();
        services.AddSingleton<IBotCardStrategy, RecyclerStrategy>();
        services.AddSingleton<IBotCardStrategy, MysticDrawStrategy>();
        services.AddSingleton<IBotCardStrategy, DoubleOrNothingStrategy>();
        services.AddSingleton<IBotCardStrategy, GamblersRuinStrategy>();
        services.AddSingleton<IBotCardStrategy, ExcavatorStrategy>();
        services.AddSingleton<IBotCardStrategy, ThermalVisionStrategy>();
        services.AddSingleton<IBotCardStrategy, ChaosDiamondStrategy>();
        services.AddSingleton<IBotCardStrategy, ChaosScoutStrategy>();
        services.AddSingleton<IBotCardStrategy, MineClusterStrategy>();
        services.AddSingleton<IBotCardStrategy, CarpetBombStrategy>();
        services.AddSingleton<IBotCardStrategy, FortuneBlastStrategy>();
        services.AddSingleton<IBotCardStrategy, ChaosFogStrategy>();

        services.AddSingleton<IBotCardStrategy, FrostStrategy>();
        services.AddSingleton<IBotCardStrategy, BlackoutStrategy>();
        services.AddSingleton<IBotCardStrategy, FortuneCookieStrategy>();
        services.AddSingleton<IBotCardStrategy, SalvageStrategy>();
        services.AddSingleton<IBotCardStrategy, CardThiefStrategy>();
        services.AddSingleton<IBotCardStrategy, SabotageDeckStrategy>();
        services.AddSingleton<IBotCardStrategy, DudStrategy>();
        services.AddSingleton<IBotCardStrategy, SoulLinkStrategy>();
        services.AddSingleton<IBotCardStrategy, MirrorMatchStrategy>();
        services.AddSingleton<IBotCardStrategy, DimensionRiftStrategy>();

        return services;
    }
}