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
    [DisallowMultipleComponent]
    public class GameStartup : MonoBehaviour
    {
        [SerializeField] private AssetsStorage _internal;

        private void Awake()
        {
            Setup().Forget();
        }

        private async UniTask Setup()
        {
            var internalScopeLoader = new InternalScopeLoader(_internal);

            var internalScope = await internalScopeLoader.Load();
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