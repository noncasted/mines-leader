using Cysharp.Threading.Tasks;
using Internal;

namespace Meta
{
    public static class MetaScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadMeta(this IServiceScopeLoader loader, ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(
                parent,
                "Meta_Services",
                Construct,
                false);

            using var stage = GameProfiler.Scope("Meta");

            var scope = await loader.Load(options);

            using (GameProfiler.Scope("Loaded"))
                await scope.Initialize();

            return scope;

            UniTask Construct(IScopeBuilder builder)
            {
                using (GameProfiler.Scope("Services"))
                    builder.AddMetaServices();

                return UniTask.CompletedTask;
            }
        }
    }
}