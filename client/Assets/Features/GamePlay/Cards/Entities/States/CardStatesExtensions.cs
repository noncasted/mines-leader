using System;
using Assets.Meta;
using Global.GameServices;
using Internal;
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
                .WithParameter<IReadOnlyLifetime>(builder.Lifetime)
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

            builder.Register<CardRemoteUse>()
                .As<ICardRemoteUse>();

            return builder;
        }
        
        public static IRegistration AddCardAction(this IEntityBuilder builder, ICardDefinition definition)
        {
            var type = definition.Type;

            var registration = type switch
            {
                CardType.Trebuchet => builder.Register<CardTrebuchetAction>(),
                CardType.Bloodhound => builder.Register<CardBloodhoundAction>(),
                CardType.TrebuchetAimer => builder.Register<CardTrebuchetAimerAction>(),
                CardType.ErosionDozer => builder.Register<CardErosionDozerAction>(),
                CardType.Gravedigger => builder.Register<CardGravediggerAction>(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            registration.As<ICardAction>();

            return registration;
        }
    }
}