using System;
using Cysharp.Threading.Tasks;
using GamePlay.UI;
using Internal;
using Network;
using Shared;
using Tools.SceneBuilder;

namespace GamePlay.Loop
{
    public static class PvPScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadPvp(
            this IServiceScopeLoader loader,
            ILoadedScope parent,
            SharedMatchmaking.MatchResult sessionData)
        {
            var options = new ScopeLoadOptions(
                parent,
                Scenes.GameServices.Value,
                builder => Construct(builder, sessionData),
                false);

            var scope = await loader.Load(options);
            await scope.Initialize();

            return scope;
        }

        public static async UniTask<ILoadedScope> LoadPvPMock(
            this IServiceScopeLoader loader,
            ILoadedScope parent,
            SharedMatchmaking.MatchResult sessionData)
        {
            var options = new ScopeLoadOptions(
                parent,
                Scenes.GameServices.Value,
                builder => Construct(builder, sessionData),
                true);

            var scope = await loader.Load(options);
            await scope.Initialize();

            return scope;
        }

        private static UniTask Construct(IScopeBuilder builder, SharedMatchmaking.MatchResult sessionData)
        {
            builder.AddDefaultGamePlayServices();
            builder.AddGameEndServices();

            builder.Register<GameServicesInitializer>();

            builder.Register<PvPGameLoop>()
                   .As<IPvPGameLoop>();

            builder.AddNetworkService<GameState>("game-flow")
                   .WithProperty<GameFlowState>(1)
                   .Registration.As<IGameState>();

            builder.Register<MatchEventLoop>()
                   .As<IScopeSetup>();

            switch (sessionData.Type)
            {
                case GameMatchType.TimeLimited:
                    builder.AddNetworkService<TimeLimitedGameRound>("game-round")
                           .WithProperty<TimeLimitedRoundState>(1)
                           .Registration.As<IGameRound>();
                    break;
                case GameMatchType.LastManStanding:
                    builder.AddNetworkService<LastManStandingRound>("game-round")
                           .WithProperty<LastManStandingRoundState>(1)
                           .Registration.As<IGameRound>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return builder.AddScene();
        }

        private static UniTask AddScene(this IScopeBuilder builder)
        {
            return UniTask.WhenAll(
                builder.FindOrLoadSceneWithServices(Scenes.GameField.Value),
                builder.FindOrLoadSceneWithServices(Scenes.GameOverlay.Value),
                builder.FindOrLoadSceneWithServices(Scenes.GamePause.Value),
                builder.FindOrLoadSceneWithServices(Scenes.GameEnd.Value),
                builder.FindOrLoadSceneWithServices(Scenes.GameCheats.Value));
        }
    }
}