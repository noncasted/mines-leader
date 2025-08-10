using Common.Network;
using Internal;
using Meta;
using Shared;

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

            builder.Register<PlayerMoves>()
                .As<IPlayerMoves>()
                .As<IScopeLoaded>();

            builder.Register<PlayerModifiers>()
                .As<IPlayerModifiers>()
                .As<IScopeLoaded>();
            
            builder.RegisterProperty<PlayerManaState>();
            builder.RegisterProperty<PlayerHealthState>();
            builder.RegisterProperty<PlayerMovesState>();
            
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