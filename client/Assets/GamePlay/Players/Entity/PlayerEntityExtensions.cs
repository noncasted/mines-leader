using GamePlay.Cards;
using Internal;
using Shared;

namespace GamePlay.Players
{
    public static class PlayerEntityExtensions
    {
        public static IEntityBuilder AddPlayerComponents(this IEntityBuilder builder)
        {
            builder.Register<PlayerMana>()
                   .As<IPlayerMana>();

            builder.Register<PlayerHealth>()
                   .As<IPlayerHealth>();

            builder.Register<PlayerTurns>()
                   .As<IPlayerTurns>();

            builder.Register<PlayerModifiers>()
                   .As<IPlayerModifiers>();

            builder.Register<CardTable>()
                   .As<ICardTable>();

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