using System;
using GamePlay.Cards.Drop;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    public static class CardStatesExtensions
    {
        public static IEntityBuilder AddCardLocalStates(this IEntityBuilder builder)
        {
            builder.Register<CardLocalDrop>()
                   .WithAsset<CardLocalDropOptions>()
                   .As<ICardLocalDrop>();

            builder.Register<CardLocalStash>()
                   .WithAsset<CardLocalStashOptions>()
                   .As<ICardLocalStash>()
                   .As<ICardStash>();

            builder.Register<CardLocalIdle>()
                   .WithAsset<CardIdleOptions>()
                   .As<ICardLocalIdle>();

            builder.Register<CardLocalDrag>()
                   .WithAsset<CardDragOptions>()
                   .As<ICardLocalDrag>();

            builder.Register<CardLocalSpawn>()
                   .WithAsset<CardLocalSpawnOptions>()
                   .WithParameter(builder.Lifetime)
                   .As<ICardLocalSpawn>();

            return builder;
        }

        public static IEntityBuilder AddCardRemoteStates(this IEntityBuilder builder)
        {
            builder.Register<CardRemoteDrop>()
                   .WithAsset<CardRemoteDropOptions>()
                   .As<ICardRemoteDrop>();

            builder.Register<CardRemoteStash>()
                   .WithAsset<CardRemoteStashOptions>()
                   .As<ICardRemoteStash>()
                   .As<ICardStash>();

            builder.Register<CardRemoteIdle>()
                   .WithAsset<CardRemoteIdleOptions>()
                   .As<ICardRemoteIdle>();

            builder.Register<CardRemoteSpawn>()
                   .WithAsset<CardRemoteSpawnOptions>()
                   .As<ICardRemoteSpawn>();

            return builder;
        }

        public static void AddCardAction(
            this IEntityBuilder builder,
            CardConfigOptions configs,
            ICardDefinition definition)
        {
            var type = definition.Type;

            var registration = type switch
            {
                CardType.Trebuchet => builder.Register<CardTrebuchetAction>(),
                CardType.Trebuchet_Max => builder.Register<CardTrebuchetAction>(),
                CardType.Bloodhound => builder.Register<CardBloodhoundAction>(),
                CardType.Bloodhound_Max => builder.Register<CardBloodhoundAction>(),
                CardType.TrebuchetAimer => builder.Register<CardTrebuchetAimerAction>(),
                CardType.TrebuchetAimer_Max => builder.Register<CardTrebuchetAimerAction>(),
                CardType.ErosionDozer => builder.Register<CardErosionDozerAction>(),
                CardType.ErosionDozer_Max => builder.Register<CardErosionDozerAction>(),
                CardType.Gravedigger => builder.Register<CardGravediggerAction>(),
                CardType.ZipZap => builder.Register<CardZipZapAction>(),
                CardType.ZipZap_Max => builder.Register<CardZipZapAction>(),
                CardType.OpponentBomb => builder.Register<CardOpponentBombAction>(),
                CardType.OpponentFlagErase => builder.Register<CardOpponentFlagEraseAction>(),
                CardType.OpponentFlagErase_Max => builder.Register<CardOpponentFlagEraseAction>(),
                CardType.OpponentFlagReshuffle => builder.Register<CardOpponentFlagReshuffleAction>(),
                CardType.OpponentFlagReshuffle_Max => builder.Register<CardOpponentFlagReshuffleAction>(),
                CardType.Smoke => builder.Register<CardSmokeAction>(),
                CardType.Smoke_Max => builder.Register<CardSmokeAction>(),
                CardType.Medic => builder.Register<CardMedicAction>(),
                CardType.MinefieldScout => builder.Register<CardMinefieldScoutAction>(),
                CardType.MinefieldScout_Max => builder.Register<CardMinefieldScoutAction>(),
                CardType.Siphon => builder.Register<CardSiphonAction>(),
                CardType.ChainReaction => builder.Register<CardChainReactionAction>(),
                CardType.Overclock => builder.Register<CardOverclockAction>(),
                CardType.FogOfWar => builder.Register<CardFogOfWarAction>(),
                CardType.FogOfWar_Max => builder.Register<CardFogOfWarAction>(),
                CardType.Scavenger => builder.Register<CardScavengerAction>(),
                CardType.HandScramble => builder.Register<CardHandScrambleAction>(),
                CardType.Lockdown => builder.Register<CardLockdownAction>(),
                CardType.Sonar => builder.Register<CardSonarAction>(),
                CardType.Purge => builder.Register<CardPurgeAction>(),
                CardType.Adrenaline => builder.Register<CardAdrenalineAction>(),
                CardType.ManaSurge => builder.Register<CardManaSurgeAction>(),
                CardType.BloodPact => builder.Register<CardBloodPactAction>(),
                CardType.CoinToss => builder.Register<CardCoinTossAction>(),
                CardType.ManaFountain => builder.Register<CardManaFountainAction>(),
                CardType.Focus => builder.Register<CardFocusAction>(),
                CardType.Shield => builder.Register<CardShieldAction>(),
                CardType.PowerSurge => builder.Register<CardPowerSurgeAction>(),
                CardType.Embargo => builder.Register<CardEmbargoAction>(),
                CardType.Recycler => builder.Register<CardRecyclerAction>(),
                CardType.MysticDraw => builder.Register<CardMysticDrawAction>(),
                CardType.DoubleOrNothing => builder.Register<CardDoubleOrNothingAction>(),
                CardType.GamblersRuin => builder.Register<CardGamblersRuinAction>(),
                CardType.Excavator => builder.Register<CardExcavatorAction>(),
                CardType.Excavator_Max => builder.Register<CardExcavatorAction>(),
                CardType.ThermalVision => builder.Register<CardThermalVisionAction>(),
                CardType.ThermalVision_Max => builder.Register<CardThermalVisionAction>(),
                CardType.ChaosDiamond => builder.Register<CardChaosDiamondAction>(),
                CardType.ChaosScout => builder.Register<CardChaosScoutAction>(),
                CardType.MineCluster => builder.Register<CardMineClusterAction>(),
                CardType.MineCluster_Max => builder.Register<CardMineClusterAction>(),
                CardType.CarpetBomb => builder.Register<CardCarpetBombAction>(),
                CardType.CarpetBomb_Max => builder.Register<CardCarpetBombAction>(),
                CardType.FortuneBlast => builder.Register<CardFortuneBlastAction>(),
                CardType.ChaosFog => builder.Register<CardChaosFogAction>(),
                CardType.Frost => builder.Register<CardFrostAction>(),
                CardType.Frost_Max => builder.Register<CardFrostAction>(),
                CardType.Blackout => builder.Register<CardBlackoutAction>(),
                CardType.Blackout_Max => builder.Register<CardBlackoutAction>(),
                CardType.FortuneCookie => builder.Register<CardFortuneCookieAction>(),
                CardType.Salvage => builder.Register<CardSalvageAction>(),
                CardType.CardThief => builder.Register<CardCardThiefAction>(),
                CardType.SabotageDeck => builder.Register<CardSabotageDeckAction>(),
                CardType.Dud => builder.Register<CardDudAction>(),
                CardType.SoulLink => builder.Register<CardSoulLinkAction>(),
                CardType.MirrorMatch => builder.Register<CardMirrorMatchAction>(),
                CardType.DimensionRift => builder.Register<CardDimensionRiftAction>(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            _ = type switch
            {
                CardType.Trebuchet => registration.WithParameter(configs.Trebuchet_Normal),
                CardType.Trebuchet_Max => registration.WithParameter(configs.Trebuchet_Max),
                CardType.Bloodhound => registration.WithParameter(configs.BloodHound_Normal),
                CardType.Bloodhound_Max => registration.WithParameter(configs.BloodHound_Max),
                CardType.TrebuchetAimer => registration.WithParameter(configs.TrebuchetAimer_Normal),
                CardType.TrebuchetAimer_Max => registration.WithParameter(configs.TrebuchetAimer_Max),
                CardType.ErosionDozer => registration.WithParameter(configs.ErosionDozer_Normal),
                CardType.ErosionDozer_Max => registration.WithParameter(configs.ErosionDozer_Max),
                CardType.Gravedigger => registration.WithParameter(configs.Gravedigger_Normal),
                CardType.ZipZap => registration.WithParameter(configs.ZipZap_Normal),
                CardType.ZipZap_Max => registration.WithParameter(configs.ZipZap_Max),
                CardType.OpponentBomb => registration.WithParameter(configs.OpponentBomb_Normal),
                CardType.OpponentFlagErase => registration.WithParameter(configs.OpponentFlagErase_Normal),
                CardType.OpponentFlagErase_Max => registration.WithParameter(configs.OpponentFlagErase_Max),
                CardType.OpponentFlagReshuffle => registration.WithParameter(configs.OpponentFlagReshuffle_Normal),
                CardType.OpponentFlagReshuffle_Max => registration.WithParameter(configs.OpponentFlagReshuffle_Max),
                CardType.Smoke => registration.WithParameter(configs.Smoke_Normal),
                CardType.Smoke_Max => registration.WithParameter(configs.Smoke_Max),
                CardType.Medic => registration.WithParameter(configs.Medic_Normal),
                CardType.MinefieldScout => registration.WithParameter(configs.MinefieldScout_Normal),
                CardType.MinefieldScout_Max => registration.WithParameter(configs.MinefieldScout_Max),
                CardType.Siphon => registration.WithParameter(configs.Siphon_Normal),
                CardType.ChainReaction => registration.WithParameter(configs.ChainReaction_Normal),
                CardType.Overclock => registration.WithParameter(configs.Overclock_Normal),
                CardType.FogOfWar => registration.WithParameter(configs.FogOfWar_Normal),
                CardType.FogOfWar_Max => registration.WithParameter(configs.FogOfWar_Max),
                CardType.Scavenger => registration.WithParameter(configs.Scavenger_Normal),
                CardType.HandScramble => registration.WithParameter(configs.HandScramble_Normal),
                CardType.Lockdown => registration.WithParameter(configs.Lockdown_Normal),
                CardType.Sonar => registration.WithParameter(configs.Sonar_Normal),
                CardType.Purge => registration.WithParameter(configs.Purge_Normal),
                CardType.Adrenaline => registration.WithParameter(configs.Adrenaline_Normal),
                CardType.ManaSurge => registration.WithParameter(configs.ManaSurge_Normal),
                CardType.BloodPact => registration.WithParameter(configs.BloodPact_Normal),
                CardType.CoinToss => registration.WithParameter(configs.CoinToss_Normal),
                CardType.ManaFountain => registration.WithParameter(configs.ManaFountain_Normal),
                CardType.Focus => registration.WithParameter(configs.Focus_Normal),
                CardType.Shield => registration.WithParameter(configs.Shield_Normal),
                CardType.PowerSurge => registration.WithParameter(configs.PowerSurge_Normal),
                CardType.Embargo => registration.WithParameter(configs.Embargo_Normal),
                CardType.Recycler => registration.WithParameter(configs.Recycler_Normal),
                CardType.MysticDraw => registration.WithParameter(configs.MysticDraw_Normal),
                CardType.DoubleOrNothing => registration.WithParameter(configs.DoubleOrNothing_Normal),
                CardType.GamblersRuin => registration.WithParameter(configs.GamblersRuin_Normal),
                CardType.Excavator => registration.WithParameter(configs.Excavator_Normal),
                CardType.Excavator_Max => registration.WithParameter(configs.Excavator_Max),
                CardType.ThermalVision => registration.WithParameter(configs.ThermalVision_Normal),
                CardType.ThermalVision_Max => registration.WithParameter(configs.ThermalVision_Max),
                CardType.ChaosDiamond => registration.WithParameter(configs.ChaosDiamond_Normal),
                CardType.ChaosScout => registration.WithParameter(configs.ChaosScout_Normal),
                CardType.MineCluster => registration.WithParameter(configs.MineCluster_Normal),
                CardType.MineCluster_Max => registration.WithParameter(configs.MineCluster_Max),
                CardType.CarpetBomb => registration.WithParameter(configs.CarpetBomb_Normal),
                CardType.CarpetBomb_Max => registration.WithParameter(configs.CarpetBomb_Max),
                CardType.FortuneBlast => registration.WithParameter(configs.FortuneBlast_Normal),
                CardType.ChaosFog => registration.WithParameter(configs.ChaosFog_Normal),
                CardType.Frost => registration.WithParameter(configs.Frost_Normal),
                CardType.Frost_Max => registration.WithParameter(configs.Frost_Max),
                CardType.Blackout => registration.WithParameter(configs.Blackout_Normal),
                CardType.Blackout_Max => registration.WithParameter(configs.Blackout_Max),
                CardType.FortuneCookie => registration.WithParameter(configs.FortuneCookie_Normal),
                CardType.Salvage => registration.WithParameter(configs.Salvage_Normal),
                CardType.CardThief => registration.WithParameter(configs.CardThief_Normal),
                CardType.SabotageDeck => registration.WithParameter(configs.SabotageDeck_Normal),
                CardType.Dud => registration.WithParameter(configs.Dud_Normal),
                CardType.SoulLink => registration.WithParameter(configs.SoulLink_Normal),
                CardType.MirrorMatch => registration.WithParameter(configs.MirrorMatch_Normal),
                CardType.DimensionRift => registration.WithParameter(configs.DimensionRift_Normal),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };


            registration.As<ICardAction>();

            IRegistration Register<T, TConfig>(TConfig config)
            {
                return builder.Register<T>()
                              .WithParameter(config);
            }
        }

        public static void AddCardActionSync(this IBuilder builder, ICardDefinition definition)
        {
            var type = definition.Type;

            _ = type switch
            {
                CardType.Trebuchet => Sync<CardTrebuchetAction.Snapshot, CardActionSnapshot.Trebuchet>(),
                CardType.Trebuchet_Max => Sync<CardTrebuchetAction.Snapshot, CardActionSnapshot.Trebuchet>(),
                CardType.Bloodhound => Sync<CardBloodhoundAction.Snapshot, CardActionSnapshot.Bloodhound>(),
                CardType.Bloodhound_Max => Sync<CardBloodhoundAction.Snapshot, CardActionSnapshot.Bloodhound>(),
                CardType.TrebuchetAimer => Sync<CardTrebuchetAimerAction.Snapshot, CardActionSnapshot.TrebuchetAimer>(),
                CardType.TrebuchetAimer_Max =>
                    Sync<CardTrebuchetAimerAction.Snapshot, CardActionSnapshot.TrebuchetAimer>(),
                CardType.ErosionDozer => Sync<CardErosionDozerAction.Snapshot, CardActionSnapshot.ErosionDozer>(),
                CardType.ErosionDozer_Max => Sync<CardErosionDozerAction.Snapshot, CardActionSnapshot.ErosionDozer>(),
                CardType.Gravedigger => Sync<CardGravediggerAction.Snapshot, CardActionSnapshot.Gravedigger>(),
                CardType.ZipZap => Sync<CardZipZapAction.Snapshot, CardActionSnapshot.ZipZap>()
                    .WithAsset<ZipZapOptions>(),
                CardType.ZipZap_Max => Sync<CardZipZapAction.Snapshot, CardActionSnapshot.ZipZap>()
                    .WithAsset<ZipZapOptions>(),
                CardType.OpponentBomb => Sync<CardOpponentBombAction.Snapshot, CardActionSnapshot.OpponentBomb>(),
                CardType.OpponentFlagErase =>
                    Sync<CardOpponentFlagEraseAction.Snapshot, CardActionSnapshot.OpponentFlagErase>(),
                CardType.OpponentFlagErase_Max =>
                    Sync<CardOpponentFlagEraseAction.Snapshot, CardActionSnapshot.OpponentFlagErase>(),
                CardType.OpponentFlagReshuffle => Sync<CardOpponentFlagReshuffleAction.Snapshot,
                    CardActionSnapshot.OpponentFlagReshuffle>(),
                CardType.OpponentFlagReshuffle_Max => Sync<CardOpponentFlagReshuffleAction.Snapshot,
                    CardActionSnapshot.OpponentFlagReshuffle>(),
                CardType.Smoke => Sync<CardSmokeAction.Snapshot, CardActionSnapshot.Smoke>(),
                CardType.Smoke_Max => Sync<CardSmokeAction.Snapshot, CardActionSnapshot.Smoke>(),
                CardType.Medic => Sync<CardMedicAction.Snapshot, CardActionSnapshot.Medic>(),
                CardType.MinefieldScout => Sync<CardMinefieldScoutAction.Snapshot, CardActionSnapshot.MinefieldScout>(),
                CardType.MinefieldScout_Max =>
                    Sync<CardMinefieldScoutAction.Snapshot, CardActionSnapshot.MinefieldScout>(),
                CardType.Siphon => Sync<CardSiphonAction.Snapshot, CardActionSnapshot.Siphon>(),
                CardType.ChainReaction => Sync<CardChainReactionAction.Snapshot, CardActionSnapshot.ChainReaction>(),
                CardType.Overclock => Sync<CardOverclockAction.Snapshot, CardActionSnapshot.Overclock>(),
                CardType.FogOfWar => Sync<CardFogOfWarAction.Snapshot, CardActionSnapshot.FogOfWar>(),
                CardType.FogOfWar_Max => Sync<CardFogOfWarAction.Snapshot, CardActionSnapshot.FogOfWar>(),
                CardType.Scavenger => Sync<CardScavengerAction.Snapshot, CardActionSnapshot.Scavenger>(),
                CardType.HandScramble => Sync<CardHandScrambleAction.Snapshot, CardActionSnapshot.HandScramble>(),
                CardType.Lockdown => Sync<CardLockdownAction.Snapshot, CardActionSnapshot.Lockdown>(),
                CardType.Sonar => Sync<CardSonarAction.Snapshot, CardActionSnapshot.Sonar>(),
                CardType.Purge => Sync<CardPurgeAction.Snapshot, CardActionSnapshot.Purge>(),
                CardType.Adrenaline => Sync<CardAdrenalineAction.Snapshot, CardActionSnapshot.Adrenaline>(),
                CardType.ManaSurge => Sync<CardManaSurgeAction.Snapshot, CardActionSnapshot.ManaSurge>(),
                CardType.BloodPact => Sync<CardBloodPactAction.Snapshot, CardActionSnapshot.BloodPact>(),
                CardType.CoinToss => Sync<CardCoinTossAction.Snapshot, CardActionSnapshot.CoinToss>(),
                CardType.ManaFountain => Sync<CardManaFountainAction.Snapshot, CardActionSnapshot.ManaFountain>(),
                CardType.Focus => Sync<CardFocusAction.Snapshot, CardActionSnapshot.Focus>(),
                CardType.Shield => Sync<CardShieldAction.Snapshot, CardActionSnapshot.Shield>(),
                CardType.PowerSurge => Sync<CardPowerSurgeAction.Snapshot, CardActionSnapshot.PowerSurge>(),
                CardType.Embargo => Sync<CardEmbargoAction.Snapshot, CardActionSnapshot.Embargo>(),
                CardType.Recycler => Sync<CardRecyclerAction.Snapshot, CardActionSnapshot.Recycler>(),
                CardType.MysticDraw => Sync<CardMysticDrawAction.Snapshot, CardActionSnapshot.MysticDraw>(),
                CardType.DoubleOrNothing => Sync<CardDoubleOrNothingAction.Snapshot, CardActionSnapshot.DoubleOrNothing>(),
                CardType.GamblersRuin => Sync<CardGamblersRuinAction.Snapshot, CardActionSnapshot.GamblersRuin>(),
                CardType.Excavator => Sync<CardExcavatorAction.Snapshot, CardActionSnapshot.Excavator>(),
                CardType.Excavator_Max => Sync<CardExcavatorAction.Snapshot, CardActionSnapshot.Excavator>(),
                CardType.ThermalVision => Sync<CardThermalVisionAction.Snapshot, CardActionSnapshot.ThermalVision>(),
                CardType.ThermalVision_Max => Sync<CardThermalVisionAction.Snapshot, CardActionSnapshot.ThermalVision>(),
                CardType.ChaosDiamond => Sync<CardChaosDiamondAction.Snapshot, CardActionSnapshot.ChaosDiamond>(),
                CardType.ChaosScout => Sync<CardChaosScoutAction.Snapshot, CardActionSnapshot.ChaosScout>(),
                CardType.MineCluster => Sync<CardMineClusterAction.Snapshot, CardActionSnapshot.MineCluster>(),
                CardType.MineCluster_Max => Sync<CardMineClusterAction.Snapshot, CardActionSnapshot.MineCluster>(),
                CardType.CarpetBomb => Sync<CardCarpetBombAction.Snapshot, CardActionSnapshot.CarpetBomb>(),
                CardType.CarpetBomb_Max => Sync<CardCarpetBombAction.Snapshot, CardActionSnapshot.CarpetBomb>(),
                CardType.FortuneBlast => Sync<CardFortuneBlastAction.Snapshot, CardActionSnapshot.FortuneBlast>(),
                CardType.ChaosFog => Sync<CardChaosFogAction.Snapshot, CardActionSnapshot.ChaosFog>(),
                CardType.Frost => Sync<CardFrostAction.Snapshot, CardActionSnapshot.Frost>(),
                CardType.Frost_Max => Sync<CardFrostAction.Snapshot, CardActionSnapshot.Frost>(),
                CardType.Blackout => Sync<CardBlackoutAction.Snapshot, CardActionSnapshot.Blackout>(),
                CardType.Blackout_Max => Sync<CardBlackoutAction.Snapshot, CardActionSnapshot.Blackout>(),
                CardType.FortuneCookie => Sync<CardFortuneCookieAction.Snapshot, CardActionSnapshot.FortuneCookie>(),
                CardType.Salvage => Sync<CardSalvageAction.Snapshot, CardActionSnapshot.Salvage>(),
                CardType.CardThief => Sync<CardCardThiefAction.Snapshot, CardActionSnapshot.CardThief>(),
                CardType.SabotageDeck => Sync<CardSabotageDeckAction.Snapshot, CardActionSnapshot.SabotageDeck>(),
                CardType.Dud => Sync<CardDudAction.Snapshot, CardActionSnapshot.Dud>(),
                CardType.SoulLink => Sync<CardSoulLinkAction.Snapshot, CardActionSnapshot.SoulLink>(),
                CardType.MirrorMatch => Sync<CardMirrorMatchAction.Snapshot, CardActionSnapshot.MirrorMatch>(),
                CardType.DimensionRift => Sync<CardDimensionRiftAction.Snapshot, CardActionSnapshot.DimensionRift>(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            IRegistration Sync<TImplementation, TData>()
                where TImplementation : ICardActionSync<TData>
                where TData : ICardActionData
            {
                return builder.AddCardActionSyncResolver<TImplementation, TData>();
            }
        }
    }
}