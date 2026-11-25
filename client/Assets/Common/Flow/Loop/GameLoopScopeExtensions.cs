using Cysharp.Threading.Tasks;
using Internal;
using Meta;

namespace Flow.Loop
{
    public static class GameLoopScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadGameLoop(this IServiceScopeLoader loader, ILoadedScope parent)
        {
            var options = ScopeLoadOptions.Create(parent, Construct, parent);

            using var stage = GameProfiler.Stage("Game loop");

            var scope = await loader.Load(options);

            scope.Resolve<GameLoop>().Start(scope.Lifetime);

            return scope;
        }

        [ContainerScopeParent(typeof(MetaScopeExtensions), nameof(MetaScopeExtensions.Construct))]
        public static UniTask Construct(IScopeBuilder builder, ILoadedScope parent)
        {
            builder.Register<GameLoop>();

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