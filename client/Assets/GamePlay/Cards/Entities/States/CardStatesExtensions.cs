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
            builder.Register<CardRemoteIdle>()
                .WithAsset<CardRemoteIdleOptions>()
                .As<ICardRemoteIdle>();

            builder.Register<CardRemoteSpawn>()
                .WithAsset<CardRemoteSpawnOptions>()
                .As<ICardRemoteSpawn>();

            return builder;
        }

        public static void AddCardAction(this IEntityBuilder builder, ICardDefinition definition)
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
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            registration.As<ICardAction>();
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