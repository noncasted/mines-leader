using Cysharp.Threading.Tasks;
using Global.GameLoops;
using Global.Setup;
using Internal;
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

            var gamePlayLoader = metaScope.Get<IGamePlayLoader>();
            await gamePlayLoader.Initialize(metaScope);
            
            await SceneManager.UnloadSceneAsync(startScene);
        }
    }
}