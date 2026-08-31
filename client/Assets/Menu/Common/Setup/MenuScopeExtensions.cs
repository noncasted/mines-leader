using Cysharp.Threading.Tasks;
using Internal;

namespace Menu.Common
{
    public static class MenuScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadMenu(
            this IServiceScopeLoader loader,
            ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(
                parent,
                "Menu_Services",
                Construct,
                false);

            using var stage = GameProfiler.Scope("Menu");

            var scope = await loader.Load(options);

            using (GameProfiler.Scope("Loaded"))
                await scope.Initialize();

            return scope;


            UniTask Construct(IScopeBuilder builder)
            {
                using (GameProfiler.Scope("Services"))
                    builder.AddMenuLoop();

                return builder.AddScene();
            }
        }

        public static async UniTask<ILoadedScope> LoadMenuMock(
            this IServiceScopeLoader loader,
            ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(
                parent,
                "Menu_Services",
                Construct,
                true);

            using var stage = GameProfiler.Scope("Menu mock");

            var scope = await loader.Load(options);

            using (GameProfiler.Scope("Loaded"))
                await scope.Initialize();

            return scope;

            UniTask Construct(IScopeBuilder builder)
            {
                using (GameProfiler.Scope("Services"))
                    builder.AddMenuLoop();

                return builder.AddScene();
            }
        }

        private static async UniTask AddScene(this IScopeBuilder builder)
        {
            // Сцены грузятся параллельно, поэтому каждая меряется своим отрезком на общем
            // родителе: по стеку такая вложенность не построилась бы.
            using var scenes = GameProfiler.Scope("Scenes");

            await UniTask.WhenAll(
                scenes.Measure("Scene: Menu", () => builder.FindOrLoadSceneWithServices(Scenes.Menu.Value)),
                scenes.Measure("Scene: MenuBoard", () => builder.FindOrLoadSceneWithServices(Scenes.MenuBoard.Value)));
        }
    }
}