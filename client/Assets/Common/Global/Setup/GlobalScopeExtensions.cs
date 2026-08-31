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
                "Global_Services",
                Construct,
                false);

            using var stage = GameProfiler.Scope("Global");

            var scope = await loader.Load(options);

            using (GameProfiler.Scope("Loaded"))
                await scope.Initialize();

            return scope;

            async UniTask Construct(IScopeBuilder builder)
            {
                // Отрезок на группу открывает сам LoadPrefabGroupNow.
                await builder.LoadPrefabGroupNow(Prefabs.Global);

                // Модули меряются поимённо: половина из них инстанцирует префабы, и по
                // трассе сразу видно, какой именно из них стоит кадров.
                using var services = GameProfiler.Scope("Services");

                services.Measure("Audio", () => builder.AddAudio());
                services.Measure("Camera", () => builder.AddCamera());
                services.Measure("Input", () => builder.AddInput());
                services.Measure("System", () => builder.AddSystemUtils());
                services.Measure("Backend", () => builder.AddBackend());
                services.Measure("Settings", () => builder.AddSettings());
                services.Measure("Publisher", () => builder.AddPublisher());
                services.Measure("UI", () => builder.AddUI());
            }
        }
    }
}