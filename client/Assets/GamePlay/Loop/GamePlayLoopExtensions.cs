using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Players;
using GamePlay.Services;
using Internal;
using Network;

namespace GamePlay.Loop
{
    public static class GamePlayLoopExtensions
    {
        public static IScopeBuilder AddDefaultGamePlayServices(this IScopeBuilder builder)
        {
            builder
                .AddGamePlayServices()
                .AddPlayerServices()
                .AddBoardServices()
                .AddCardServices()
                .AddSessionServices()
                .AddSnapshotSync();

            builder.Register<GameContext>()
                   .As<IGameContext>();

            return builder;
        }
    }
}