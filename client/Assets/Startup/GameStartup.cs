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
            var scopeLoader = internalScope.Get<IServiceScopeLoader>();

            var globalScope = await scopeLoader.LoadGlobal(internalScope);
            var globalCamera = globalScope.Get<IGlobalCamera>();;
            var loadingScreen = globalScope.Get<ILoadingScreen>();;
            globalCamera.Enable();
            loadingScreen.Show();

            var metaScope = await scopeLoader.LoadMeta(globalScope);
            
            await scopeLoader.LoadGameLoop(metaScope);

            await SceneManager.UnloadSceneAsync(startScene);
        }
    }
}