using Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace Game.GamePlay;

public static class CardServiceExtensions
{
    public static IServiceCollection AddCardServices(this IServiceCollection services)
    {
        // Buff
        services.Add<Focus>().As<ICard<CardUsePayload.Focus>>();
        services.Add<Adrenaline>().As<ICard<CardUsePayload.Adrenaline>>();
        services.Add<Overclock>().As<ICard<CardUsePayload.Overclock>>();
        services.Add<CoinToss>().As<ICard<CardUsePayload.CoinToss>>();
        services.Add<Lockdown>().As<ICard<CardUsePayload.Lockdown>>();
        services.Add<PowerSurge>().As<ICard<CardUsePayload.PowerSurge>>();
        services.Add<Shield>().As<ICard<CardUsePayload.Shield>>();
        services.Add<Medic>().As<ICard<CardUsePayload.Medic>>();
        services.Add<Purge>().As<ICard<CardUsePayload.Purge>>();

        // Resources
        services.Add<ManaSurge>().As<ICard<CardUsePayload.ManaSurge>>();
        services.Add<ManaFountain>().As<ICard<CardUsePayload.ManaFountain>>();
        services.Add<BloodPact>().As<ICard<CardUsePayload.BloodPact>>();
        services.Add<Embargo>().As<ICard<CardUsePayload.Embargo>>();
        services.Add<DoubleOrNothing>().As<ICard<CardUsePayload.DoubleOrNothing>>();
        services.Add<GamblersRuin>().As<ICard<CardUsePayload.GamblersRuin>>();
        services.Add<Siphon>().As<ICard<CardUsePayload.Siphon>>();
        services.Add<SoulLink>().As<ICard<CardUsePayload.SoulLink>>();

        // Hand
        services.Add<MysticDraw>().As<ICard<CardUsePayload.MysticDraw>>();
        services.Add<Recycler>().As<ICard<CardUsePayload.Recycler>>();
        services.Add<Scavenger>().As<ICard<CardUsePayload.Scavenger>>();
        services.Add<HandScramble>().As<ICard<CardUsePayload.HandScramble>>();
        services.Add<GraveDigger>().As<ICard<CardUsePayload.Gravedigger>>();
        services.Add<Salvage>().As<ICard<CardUsePayload.Salvage>>();
        services.Add<CardThief>().As<ICard<CardUsePayload.CardThief>>();
        services.Add<SabotageDeck>().As<ICard<CardUsePayload.SabotageDeck>>();
        services.Add<Dud>().As<ICard<CardUsePayload.Dud>>();
        services.Add<MirrorMatch>().As<ICard<CardUsePayload.MirrorMatch>>();

        // CrossBoard
        services.Add<Trebuchet>().As<ICard<CardUsePayload.Trebuchet>>();
        services.Add<TrebuchetAimer>().As<ICard<CardUsePayload.TrebuchetAimer>>();
        services.Add<OpponentBomb>().As<ICard<CardUsePayload.OpponentBomb>>();
        services.Add<OpponentFlagErase>().As<ICard<CardUsePayload.OpponentFlagErase>>();
        services.Add<OpponentFlagReshuffle>().As<ICard<CardUsePayload.OpponentFlagReshuffle>>();
        services.Add<Smoke>().As<ICard<CardUsePayload.Smoke>>();
        services.Add<FogOfWar>().As<ICard<CardUsePayload.FogOfWar>>();
        services.Add<ChaosFog>().As<ICard<CardUsePayload.ChaosFog>>();
        services.Add<Frost>().As<ICard<CardUsePayload.Frost>>();
        services.Add<Blackout>().As<ICard<CardUsePayload.Blackout>>();
        services.Add<ChainReaction>().As<ICard<CardUsePayload.ChainReaction>>();
        services.Add<MineCluster>().As<ICard<CardUsePayload.MineCluster>>();
        services.Add<CarpetBomb>().As<ICard<CardUsePayload.CarpetBomb>>();
        services.Add<FortuneBlast>().As<ICard<CardUsePayload.FortuneBlast>>();
        services.Add<DimensionRift>().As<ICard<CardUsePayload.DimensionRift>>();

        // Scout
        services.Add<Bloodhound>().As<ICard<CardUsePayload.Bloodhound>>();
        services.Add<ErosionDozer>().As<ICard<CardUsePayload.ErosionDozer>>();
        services.Add<ZipZap>().As<ICard<CardUsePayload.ZipZap>>();
        services.Add<MinefieldScout>().As<ICard<CardUsePayload.MinefieldScout>>();
        services.Add<Sonar>().As<ICard<CardUsePayload.Sonar>>();
        services.Add<Excavator>().As<ICard<CardUsePayload.Excavator>>();
        services.Add<ThermalVision>().As<ICard<CardUsePayload.ThermalVision>>();
        services.Add<ChaosDiamond>().As<ICard<CardUsePayload.ChaosDiamond>>();
        services.Add<ChaosScout>().As<ICard<CardUsePayload.ChaosScout>>();
        services.Add<FortuneCookie>().As<ICard<CardUsePayload.FortuneCookie>>();

        return services;
    }
}