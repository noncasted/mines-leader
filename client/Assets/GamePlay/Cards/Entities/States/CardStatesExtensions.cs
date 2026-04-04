using System;
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
                .WithAsset<CardDropOptions>()
                .As<ICardLocalDrop>();

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
                .WithAsset<CardDropOptions>()
                .As<ICardRemoteDrop>();

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
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };


            registration.As<ICardAction>();

            IRegistration Register<T, TConfig>(TConfig config)
            {
                return builder.Register<T>()
                    .WithParameter(config);
            }
        }

        public static void AddCardActionSync(this IEntityBuilder builder, ICardDefinition definition)
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