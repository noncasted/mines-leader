using System;
using Cysharp.Threading.Tasks;
using GamePlay.Agent;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Cheats;
using GamePlay.Metrics;
using GamePlay.Players;
using GamePlay.Services;
using GamePlay.UI;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Loop
{
    public static class GamePlayScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadPvp(
            this IServiceScopeLoader loader,
            ILoadedScope parent,
            SharedMatchmaking.MatchResult sessionData)
        {
            var options = ScopeLoadOptions.Create(parent, Construct, sessionData)
                .WithRuntimeScene("Game_Services");

            return await loader.Load(options);
        }

        public static async UniTask<ILoadedScope> LoadPvPMock(
            this IServiceScopeLoader loader,
            ILoadedScope parent,
            SharedMatchmaking.MatchResult sessionData)
        {
            var options = ScopeLoadOptions.Create(parent, Construct, sessionData)
                .WithRuntimeScene("Game_Services")
                .AsMock();

            return await loader.Load(options);
        }

        [ContainerScopeParent(typeof(MetaScopeExtensions), nameof(MetaScopeExtensions.Construct))]
        public static UniTask Construct(IScopeBuilder builder, SharedMatchmaking.MatchResult sessionData)
        {
            builder.RequestSpriteGroup(Sprites.Cards);
            builder.RequestSpriteGroup(Sprites.GameUI);
            builder.RequestSpriteGroup(Sprites.GameUIPlate);
            builder.RequestSpriteGroup(Sprites.GameCells);
            builder.RequestSpriteGroup(Sprites.GameField);
            builder.RequestSpriteGroup(Sprites.Settings);
            builder.RequestPrefabGroup(GamePlayPrefabs.Group);
            builder.RequestAudioGroup(GamePlayAudio.Group);
            builder.RequestEnvAssetGroup(GamePlayAssets.Group);
            
            builder
                .AddGamePlayServices()
                .AddGameOverlayServices()
                .AddGamePauseServices()
                .AddPlayerServices()
                .AddBoardServices()
                .AddCardServices()
                .AddSessionServices()
                .AddNetworkLatencyOverlay(InternalAssets.NetworkOverlayOptions)
                .AddSnapshotSync();

            builder.Register<GameContext>()
                   .As<IGameContext>();

            // CardVfxFactory injects VFX created at runtime.
            builder.Injectable<ZipZapLine>();
            
            builder.AddGameEndServices();

            builder.Register<GameServicesInitializer>();

            builder.Register<GamePlayLoop>()
                   .As<IGamePlayLoop>();

            builder.Register<GameState>()
                   .As<IGameState>();

            builder.Register<MatchEventLoop>()
                   .As<IScopeSetup>();

            builder.Register<GameRoundVisuals>()
                   .As<IRoundChanged>()
                   .As<IMatchCompleted>();

            // The round is an alternative of one service: the scope class builds whichever is registered.
            switch (sessionData.Type)
            {
                case GameMatchType.TimeLimited:
                    builder.Register<TimeLimitedGameRound>()
                           .As<IGameRound>();
                    break;
                case GameMatchType.LastManStanding:
                case GameMatchType.LastManStandingTurnBased:
                    builder.Register<LastManStandingRound>()
                           .As<IGameRound>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // The handlers are shared: mode records arrive only in a match of that mode.
            builder.AddSnapshotHandler<TimeLimitedRoundSnapshotHandler, TimeLimitedRoundRecord>();
            builder.AddSnapshotHandler<LastManStandingRoundSnapshotHandler, LastManStandingRoundRecord>();

            builder
                .AddGameCheats()
                .AddGameAgent();

            builder.Register<MatchTelemetryMark>()
                   .As<IGameStarted>();

            builder.AddMatchMetrics(sessionData.SessionId);

            return builder.AddScene();
        }

        private static UniTask AddScene(this IScopeBuilder builder)
        {
            return UniTask.WhenAll(
                builder.FindOrLoadSceneWithServices(Scenes.GameField),
                builder.FindOrLoadSceneWithServices(Scenes.GameOverlay),
                builder.FindOrLoadSceneWithServices(Scenes.GamePause),
                builder.FindOrLoadSceneWithServices(Scenes.GameResult));
        }
    }
}