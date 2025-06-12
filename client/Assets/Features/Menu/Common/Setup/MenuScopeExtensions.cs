using Common.Network;
using Cysharp.Threading.Tasks;
using Internal;
using Menu.Loop;
using VContainer;

namespace Menu.Setup
{
    public static class MenuScopeExtensions
    {
        public static async UniTask<(ILoadedScope, MenuResult)> ProcessMenu(
            this IServiceScopeLoader loader,
            ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(
                parent,
                loader.Assets.GetAsset<MenuServicesScene>(),
                Construct,
                false);

            var scope = await loader.Load(options);
            await scope.Initialize();

            var loop = scope.Container.Container.Resolve<IMenuLoop>();
            var mainResult = await loop.Process(scope.Lifetime);
            return (scope, mainResult);


            UniTask Construct(IScopeBuilder builder)
            {
                builder
                    .AddMenuLoop()
                    .AddSessionServices()
                    .AddMenuSocial();

                return UniTask.WhenAll(builder.AddScene());
            }
        }

        public static async UniTask<ILoadedScope> LoadMenuMock(
            this IServiceScopeLoader loader,
            ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(
                parent,
                loader.Assets.GetAsset<MenuServicesScene>(),
                Construct,
                true);

            var scope = await loader.Load(options);
            await scope.Initialize();

            return scope;


            UniTask Construct(IScopeBuilder builder)
            {
                builder
                    .AddMenuLoop()
                    .AddSessionServices()
                    .AddMenuSocial();

                return UniTask.WhenAll(builder.AddScene());
            }
        }

        private static UniTask AddScene(this IScopeBuilder builder)
        {
            return builder.FindOrLoadSceneWithServices<MenuUIScene>();
        }
    }
}