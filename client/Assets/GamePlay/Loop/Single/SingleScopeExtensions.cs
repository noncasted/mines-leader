using Cysharp.Threading.Tasks;
using Internal;
using Meta;
using VContainer;

namespace GamePlay.Loop
{
    public static class SingleScopeExtensions
    {
        public static async UniTask<ILoadedScope> ProcessSingle(
            this IServiceScopeLoader loader,
            ILoadedScope parent,
            SessionData sessionData)
        {
            var options = new ScopeLoadOptions(
                parent,
                loader.Assets.GetAsset<GamePlayServicesScene>(),
                Construct,
                false);
            
            var scope = await loader.Load(options);
            await scope.Initialize();

            var loop = scope.Container.Container.Resolve<ISingleGameLoop>();
            await loop.Process(scope.Lifetime, sessionData);

            return scope;
        }


        public static async UniTask<ILoadedScope> ProcessSingleMock(
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

            var loop = scope.Container.Container.Resolve<ISingleGameLoop>();
            await loop.Process(scope.Lifetime, sessionData);

            return scope;
        }

        private static UniTask Construct(IScopeBuilder builder)
        {
            builder.AddDefaultGamePlayServices();

            builder.Register<SingleGameLoop>()
                .As<ISingleGameLoop>();

            builder.Register<SingleGameFlow>()
                .As<IGameFlow>();

            return UniTask.WhenAll(builder.AddScene());
        }

        private static async UniTask AddScene(this IScopeBuilder builder)
        {
            await builder.FindOrLoadSceneWithServices<GamePlayScene>();
        }
    }
}