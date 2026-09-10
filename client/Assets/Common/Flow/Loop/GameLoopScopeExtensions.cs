using Cysharp.Threading.Tasks;
using Global.Setup;
using Internal;
using Meta;

namespace Flow.Loop
{
    public static class GameLoopScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadGameLoop(this IServiceScopeLoader loader, ILoadedScope parent)
        {
            var options = ScopeLoadOptions.Create(
                parent,
                "GameLoop_Services",
                Construct,
                parent,
                false);

            using var stage = GameProfiler.Scope("Game loop");

            var scope = await loader.Load(options);

            using (GameProfiler.Scope("Loaded"))
                await scope.Initialize();

            return scope;
        }

        [ContainerScopeParent(typeof(MetaScopeExtensions), nameof(MetaScopeExtensions.Construct))]
        public static UniTask Construct(IScopeBuilder builder, ILoadedScope parent)
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