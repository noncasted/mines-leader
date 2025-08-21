using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Cheats;
using GamePlay.UI;
using Global.Backend;
using Internal;
using Meta;
using Shared;
using VContainer;

namespace GamePlay.Loop
{
    public static class PvPScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadPvp(
            this IServiceScopeLoader loader,
            ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(
                parent,
                loader.Assets.GetAsset<GameServicesScene>(),
                Construct,
                false
            );

            var scope = await loader.Load(options);
            await scope.Initialize();

            return scope;
        }

        public static async UniTask<ILoadedScope> ProcessPvPMock(
            this IServiceScopeLoader loader,
            ILoadedScope parent,
            SessionData sessionData)
        {
            var options = new ScopeLoadOptions(
                parent,
                loader.Assets.GetAsset<GameServicesScene>(),
                Construct,
                true
            );

            var scope = await loader.Load(options);
            await scope.Initialize();

            var loop = scope.Container.Container.Resolve<IPvPGameLoop>();
            await loop.Process(scope.Lifetime, sessionData);
            return scope;
        }

        private static UniTask Construct(IScopeBuilder builder)
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


            return builder.AddScene();
        }

        private static UniTask AddScene(this IScopeBuilder builder)
        {
            return UniTask.WhenAll(
                builder.FindOrLoadSceneWithServices<GameFieldScene>(),
                builder.FindOrLoadSceneWithServices<GameOverlayScene>(),
                builder.FindOrLoadSceneWithServices<GamePauseScene>(),
                builder.FindOrLoadSceneWithServices<GameEndScene>(),
                builder.FindOrLoadSceneWithServices<GameCheatsScene>()
            );
        }
    }
}