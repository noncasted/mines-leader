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

            using var stage = GameProfiler.Stage("Global");

            return await loader.Load(options);
        }

        [ContainerScopeParent(typeof(InternalScopeExtensions), nameof(InternalScopeExtensions.Construct))]
        public static async UniTask Construct(IScopeBuilder builder)
        {
            // Options (AddPublisher) and prefabs (Instantiate in Add*) are needed right at registration,
            // so both groups load before it, in parallel with each other.
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
            await builder.AddPublisher();
            builder.AddGlobalUI();
        }
    }
}