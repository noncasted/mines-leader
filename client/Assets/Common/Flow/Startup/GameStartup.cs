using Cysharp.Threading.Tasks;
using Flow.Loop;
using Global.Cameras;
using Global.Setup;
using Global.UI;
using Internal;
using Meta;
using UnityEngine;

namespace Flow.Startup
{
    public class GameStartup : StartupEntry
    {
        private void Awake()
        {
            Setup().Forget();
        }

        private async UniTask Setup()
        {
            // The startup trace is closed on entering the menu: the loading ends there,
            // and control never returns here (see MenuLoader).
            GameProfiler.Begin("Startup");

            // The root is built by the same loader as the other scopes; later it is taken from the container.
            var serviceLoader = new ServiceScopeLoader();
            var internalScope = await serviceLoader.LoadInternal();

            // Meta sprites download in parallel with the rest of the loading: the meta waits for them itself.
            internalScope.Resolve<IStartupAssetsPreload>().Start();

            var scopeLoader = internalScope.Resolve<IServiceScopeLoader>();

            var globalScope = await scopeLoader.LoadGlobal(internalScope);
            var globalCamera = globalScope.Resolve<IGlobalCamera>();
            var loadingScreen = globalScope.Resolve<ILoadingScreen>();

            globalCamera.Enable();
            loadingScreen.ShowInstantly();

            var metaScope = await scopeLoader.LoadMeta(globalScope);

            await scopeLoader.LoadGameLoop(metaScope);
        }
    }
}