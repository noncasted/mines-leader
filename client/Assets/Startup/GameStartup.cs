using Cysharp.Threading.Tasks;
using Global.Cameras;
using Global.Setup;
using Global.UI;
using Internal;
using Loop;
using Meta;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Startup
{
    [DisallowMultipleComponent]
    public class GameStartup : MonoBehaviour
    {
        [SerializeField] private InternalScopeConfig _internal;

        private void Awake()
        {
            Setup().Forget();
        }

        private async UniTask Setup()
        {
            var internalScopeLoader = new InternalScopeLoader(_internal);
            var startScene = gameObject.scene;

            var internalScope = internalScopeLoader.Load();
            var scopeLoader = internalScope.Resolve<IServiceScopeLoader>();

            var globalScope = await scopeLoader.LoadGlobal(internalScope);
            var globalCamera = globalScope.Resolve<IGlobalCamera>();
            ;
            var loadingScreen = globalScope.Resolve<ILoadingScreen>();
            ;
            globalCamera.Enable();
            loadingScreen.Show();

            var metaScope = await scopeLoader.LoadMeta(globalScope);

            await scopeLoader.LoadGameLoop(metaScope);

            await SceneManager.UnloadSceneAsync(startScene);
        }
    }
}