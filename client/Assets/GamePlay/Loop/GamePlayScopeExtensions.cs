using System;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Cards;
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

            var scope = await loader.Load(options);
            await scope.Initialize();

            return scope;
        }

        public static async UniTask<ILoadedScope> LoadPvPMock(
            this IServiceScopeLoader loader,
            ILoadedScope parent,
            SharedMatchmaking.MatchResult sessionData)
        {
            var options = ScopeLoadOptions.Create(parent, Construct, sessionData)
                .WithRuntimeScene("Game_Services")
                .AsMock();

            var scope = await loader.Load(options);
            await scope.Initialize();

            return scope;
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
                .AddSnapshotSync();

            builder.Register<GameContext>()
                   .As<IGameContext>();

            // CardVfxFactory инжектит VFX, созданные в рантайме.
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

            // Раунд — альтернатива одного сервиса: класс скоупа строит тот, что зарегистрирован.
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

            // Обработчики общие: записи режима приходят только в матче этого режима.
            builder.AddSnapshotHandler<TimeLimitedRoundSnapshotHandler, TimeLimitedRoundRecord>();
            builder.AddSnapshotHandler<LastManStandingRoundSnapshotHandler, LastManStandingRoundRecord>();

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
                builder.FindOrLoadSceneWithServices(Scenes.GameResult.Value));
        }
    }
}