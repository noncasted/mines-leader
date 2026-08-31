using Cysharp.Threading.Tasks;
using Internal;

namespace Flow.Loop
{
    public static class GameLoopScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadGameLoop(this IServiceScopeLoader loader, ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(
                parent,
                "GameLoop_Services",
                Construct,
                false);

            using var stage = GameProfiler.Scope("Game loop");

            var scope = await loader.Load(options);

            using (GameProfiler.Scope("Loaded"))
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