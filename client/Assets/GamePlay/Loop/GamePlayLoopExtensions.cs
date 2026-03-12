using Common.Network;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Players;
using GamePlay.Services;
using GamePlay.UI;
using Internal;

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
                .AddCardInfoService()
                .AddSessionServices()
                .AddSnapshotSync();

            builder.Register<GameContext>()
                .As<IGameContext>();

            return builder;
        }
    }
}