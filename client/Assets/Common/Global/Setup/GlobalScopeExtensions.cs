using Cysharp.Threading.Tasks;
using Global.Audio;
using Global.Backend;
using Global.Cameras;
using Global.Inputs;
using Global.Publisher;
using Global.Settings;
using Global.UI;
using Internal;
using UnityEngine.EventSystems;

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
            // Опции (AddPublisher) и префабы (Instantiate в Add*) нужны прямо при регистрации,
            // поэтому обе группы грузятся до неё, параллельно друг другу.
            await UniTask.WhenAll(
                builder.LoadEnvAssetGroup(GlobalAssets.Group),
                builder.LoadPrefabGroup(GlobalPrefabs.Group));

            builder.AddUpdater(GlobalPrefabs.GlobalUpdater);
            builder.AddAudio(GlobalPrefabs.GlobalAudioPlayer, GlobalPrefabs.GlobalAudioListener);
            builder.Register<ButtonSoundsPlayer>()
                   .As<IScopeSetup>();
            builder.AddCamera(GlobalPrefabs.GlobalCamera);
            builder.AddInput(GlobalAssets.InputOptions, GlobalPrefabs.GlobalEvents.GetComponent<EventSystem>());
            builder.AddBackend();
            builder.AddSettings();
            builder.AddPublisher();
            builder.AddGlobalUI();
        }
    }
}