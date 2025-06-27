using Common.Network;
using Cysharp.Threading.Tasks;
using Internal;
using Meta;
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
                loader.Assets.GetAsset<GamePlayServicesScene>(),
                Construct,
                false);

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
                loader.Assets.GetAsset<GamePlayServicesScene>(),
                Construct,
                true);

            var scope = await loader.Load(options);
            await scope.Initialize();

            var loop = scope.Container.Container.Resolve<IPvPGameLoop>();
            await loop.Process(scope.Lifetime, sessionData);
            return scope;
        }

        private static UniTask Construct(IScopeBuilder builder)
        {
            builder.AddDefaultGamePlayServices();

            builder.Register<PvPGameLoop>()
                .As<IPvPGameLoop>();

            builder.AddNetworkService<PvPGameFlow>("game-flow")
                .Registration.As<IGameFlow>();
            
            return UniTask.WhenAll(builder.AddScene());
        }

        private static async UniTask AddScene(this IScopeBuilder builder)
        {
            await builder.FindOrLoadSceneWithServices<GamePlayScene>();
        }
    }
}