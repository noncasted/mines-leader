using Common.Network;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Players;
using GamePlay.Services;
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
                .AddSessionServices();
            
            builder.Register<GameContext>()
                .As<IGameContext>();

            builder.Register<GameRound>()
                .As<IGameRound>()
                .AsSessionCallback<GameRound, INetworkSessionSetupCompleted>();
            
            return builder;
        }
    }
}