using Common.Network;
using Internal;
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

            builder.RegisterProperty<PlayerManaState>(PlayerStateIds.Mana);
            builder.RegisterProperty<PlayerHealthState>(PlayerStateIds.Health);
            builder.RegisterProperty<PlayerMovesState>(PlayerStateIds.Moves);
            builder.RegisterProperty<PlayerModifiersState>(PlayerStateIds.Modifiers);

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