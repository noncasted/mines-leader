using Cysharp.Threading.Tasks;
using Global.Audio;
using Global.Backend;
using Global.Cameras;
using Global.Inputs;
using Global.Publisher;
using Global.Settings;
using Global.UI;
using Internal;

namespace Global.Setup
{
    public static class GlobalScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadGlobal(this IServiceScopeLoader loader, ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(parent, Construct)
                .WithRuntimeScene("Global_Services");

            using var stage = GameProfiler.Scope("Global");

            var scope = await loader.Load(options);

            using (GameProfiler.Scope("Loaded"))
                await scope.Initialize();

            return scope;
        }

        [ContainerScopeParent(typeof(InternalScopeExtensions), nameof(InternalScopeExtensions.Construct))]
        public static async UniTask Construct(IScopeBuilder builder)
        {
            // GlobalPrefabs лежат в Resources и доступны без Retain.
            builder.AddUpdater();
            builder.AddAudio();
            builder.AddCamera();
            builder.AddInput();
            builder.AddBackend();
            builder.AddSettings();
            builder.AddPublisher();
            builder.AddUI();
        }
    }
}