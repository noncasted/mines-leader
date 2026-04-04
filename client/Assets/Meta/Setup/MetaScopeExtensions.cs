using Cysharp.Threading.Tasks;
using Internal;
using Tools.SceneBuilder;

namespace Meta
{
    public static class MetaScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadMeta(this IServiceScopeLoader loader, ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(
                parent,
                Scenes.MetaServices.Value,
                Construct,
                false
            );

            var scope = await loader.Load(options);
            await scope.Initialize();

            return scope;

            UniTask Construct(IScopeBuilder builder)
            {
                builder.AddMetaServices();
                return UniTask.CompletedTask;
            }
        }
    }
}