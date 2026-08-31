using Cysharp.Threading.Tasks;
using Global.Audio;
using Global.Backend;
using Global.Cameras;
using Global.Inputs;
using Global.Publisher;
using Global.Settings;
using Global.Systems;
using Global.UI;
using Internal;

namespace Global.Setup
{
    public static class GlobalScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadGlobal(this IServiceScopeLoader loader, ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(
                parent,
                Scenes.GlobalServices.Value,
                Construct,
                false);

            var scope = await loader.Load(options);
            await scope.Initialize();

            return scope;

            async UniTask Construct(IScopeBuilder builder)
            {
                await builder.LoadPrefabGroupNow(Prefabs.Global);

                builder
                    .AddAudio()
                    .AddCamera()
                    .AddInput()
                    .AddSystemUtils()
                    .AddBackend()
                    .AddSettings()
                    .AddPublisher()
                    .AddUI();
            }
        }
    }
}