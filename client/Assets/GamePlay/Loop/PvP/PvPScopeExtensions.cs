using System;
using Cysharp.Threading.Tasks;
using GamePlay.Agent;
using GamePlay.Cheats;
using GamePlay.Services;
using GamePlay.UI;
using Internal;
using Shared;
using Tools;

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

            builder.Register<GameState>()
                   .As<IGameState>();

            builder.Register<MatchEventLoop>()
                   .As<IScopeSetup>();

            switch (sessionData.Type)
            {
                case GameMatchType.TimeLimited:
                    builder.Register<TimeLimitedGameRound>()
                           .As<IGameRound>()
                           .As<ITimeLimitedGameRound>();
                    builder.AddSnapshotHandler<TimeLimitedRoundSnapshotHandler, TimeLimitedRoundRecord>();
                    break;
                case GameMatchType.LastManStanding:
                case GameMatchType.LastManStandingTurnBased:
                    builder.Register<LastManStandingRound>()
                           .As<IGameRound>()
                           .As<ILastManStandingRound>();
                    builder.AddSnapshotHandler<LastManStandingRoundSnapshotHandler, LastManStandingRoundRecord>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            builder.Register<GameCheatsService>()
                   .As<IScopeSetup>();

            builder.Register<GameAgentService>()
                   .As<IScopeSetup>();

            return builder.AddScene();
        }

        private static UniTask AddScene(this IScopeBuilder builder)
        {
            return UniTask.WhenAll(
                builder.FindOrLoadSceneWithServices(Scenes.GameField.Value),
                builder.FindOrLoadSceneWithServices(Scenes.GameOverlay.Value),
                builder.FindOrLoadSceneWithServices(Scenes.GamePause.Value),
                builder.FindOrLoadSceneWithServices(Scenes.GameEnd.Value));
        }
    }
}