using Cysharp.Threading.Tasks;
using Global.Audio;
using Global.Backend;
using Global.Cameras;
using Global.GameLoops;
using Global.GameServices;
using Global.Inputs;
using Global.Network;
using Global.Publisher;
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
                loader.Assets.GetAsset<GlobalServicesScene>(),
                Construct,
                false);
            
            var scope = await loader.Load(options);
            await scope.Initialize();

            return scope;

            UniTask Construct(IScopeBuilder builder)
            {
                builder
                    .AddAudio()
                    .AddCamera()
                    .AddInput()
                    .AddLoop()
                    .AddGameServices()
                    .AddSystemUtils()
                    .AddBackend()
                    .AddNetwork();

                return UniTask.WhenAll(
                    builder.AddPublisher(),
                    builder.AddUI());
            }
        }
    }
}