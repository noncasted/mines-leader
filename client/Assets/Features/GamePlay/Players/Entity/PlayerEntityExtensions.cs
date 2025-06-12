using System;
using Common.Network;
using Global.GameServices;
using Internal;

namespace GamePlay.Players
{
    public static class PlayerEntityExtensions
    {
        public static IEntityBuilder AddPlayerComponents(this IEntityBuilder builder)
        {
            builder.Register<PlayerMana>()
                .As<IPlayerMana>()
                .As<IScopeLoaded>();

            builder.Register<PlayerHealth>()
                .As<IPlayerHealth>()
                .As<IScopeLoaded>();

            builder.Register<PlayerTurns>()
                .As<IPlayerTurns>()
                .As<IScopeLoaded>();

            builder.Register<PlayerModifiers>()
                .As<IPlayerModifiers>();
            
            builder.RegisterProperty<PlayerManaState>();
            builder.RegisterProperty<PlayerHealthState>();
            builder.RegisterProperty<PlayerTurnsState>();
            
            return builder;
        }

        public static IEntityBuilder AddPlayerRoot(
            this IEntityBuilder builder,
            INetworkUser owner,
            CharacterType character)
        {
            builder.RegisterInstance(new GamePlayerInfo(owner.BackendId, owner.IsLocal, character))
                .As<IGamePlayerInfo>();
            
            builder.Register<GamePlayer>()
                .WithParameter(builder.Scope)
                .As<IGamePlayer>();

            return builder;
        }
    }
}