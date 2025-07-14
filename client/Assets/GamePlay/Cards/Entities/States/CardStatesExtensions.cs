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
        
        public static IRegistration AddCardAction(this IEntityBuilder builder, ICardDefinition definition)
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
                CardType.ZipZap => builder.Register<CardZipZapAction>().WithAsset<ZipZapOptions>(),
                CardType.ZipZap_Max => builder.Register<CardZipZapAction>().WithAsset<ZipZapOptions>(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            registration.As<ICardAction>();

            return registration;
        }
        
        public static IRegistration AddCardActionSync(this IEntityBuilder builder, ICardDefinition definition)
        {
            var type = definition.Type;

            var registration = type switch
            {
                CardType.Trebuchet => builder.Register<CardTrebuchetActionSync>(),
                CardType.Trebuchet_Max => builder.Register<CardTrebuchetActionSync>(),
                CardType.Bloodhound => builder.Register<CardBloodhoundActionSync>(),
                CardType.Bloodhound_Max => builder.Register<CardBloodhoundActionSync>(),
                CardType.TrebuchetAimer => builder.Register<CardTrebuchetAimerActionSync>(),
                CardType.TrebuchetAimer_Max => builder.Register<CardTrebuchetAimerActionSync>(),
                CardType.ErosionDozer => builder.Register<CardErosionDozerActionSync>(),
                CardType.ErosionDozer_Max => builder.Register<CardErosionDozerActionSync>(),
                CardType.Gravedigger => builder.Register<CardGravediggerActionSync>(),
                CardType.ZipZap => builder.Register<CardZipZapActionSync>().WithAsset<ZipZapOptions>(),
                CardType.ZipZap_Max => builder.Register<CardZipZapActionSync>().WithAsset<ZipZapOptions>(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            registration.As<ICardActionSync>();

            return registration;
        }
    }
}