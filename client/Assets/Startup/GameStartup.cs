using Cysharp.Threading.Tasks;
using Global.Setup;
using Internal;
using Loop;
using Meta;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

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
            var scopeLoader = internalScope.Container.Container.Resolve<IServiceScopeLoader>();

            var globalScope = await scopeLoader.LoadGlobal(internalScope);
            var metaScope = await scopeLoader.LoadMeta(globalScope);
            await scopeLoader.LoadGameLoop(metaScope);

            await SceneManager.UnloadSceneAsync(startScene);
        }
    }
}