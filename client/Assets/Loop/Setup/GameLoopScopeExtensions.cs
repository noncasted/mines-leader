using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Loop
{
    public static class GameLoopScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadGameLoop(this IServiceScopeLoader loader, ILoadedScope parent)
        {
            Debug.Log("add game loop");
            var options = new ScopeLoadOptions(
                parent,
                loader.Assets.GetAsset<GameLoopServicesScene>(),
                Construct,
                false);
            
            var scope = await loader.Load(options);
            await scope.Initialize();

            return scope;

            UniTask Construct(IScopeBuilder builder)
            {
                builder.Register<GameLoop>()
                    .As<IScopeLoaded>();

                builder.Register<GameLoopScopeLoader>()
                    .WithParameter(parent)
                    .As<IGameLoopScopeLoader>();

                builder.Register<MenuLoader>()
                    .As<IMenuLoader>();

                builder.Register<GamePlayLoader>()
                    .As<IGamePlayLoader>();
                
                return UniTask.CompletedTask;
            }
        }
    }
}