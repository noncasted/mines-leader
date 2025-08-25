using Common.Network;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Players;
using GamePlay.Services;
using Internal;
using Shared;

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

            builder.AddNetworkService<GameRound>("game-round")
                .WithProperty<GameRoundState>(1)
                .Registration.As<IGameRound>();

            return builder;
        }
    }
}