using Cysharp.Threading.Tasks;
using Global.GameLoops;
using Internal;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Global.Setup
{
    [DisallowMultipleComponent]
    public class GameSetup : MonoBehaviour
    {
        [SerializeField] private InternalScopeConfig _internal;

        private void Awake()
        {
            Setup().Forget();
        }

        private async UniTask Setup()
        {
            var profiler = new ProfilingScope("GameSetup");
            var internalScopeLoader = new InternalScopeLoader(_internal);
            var startScene = gameObject.scene;

            var internalScope = internalScopeLoader.Load();

            var scopeLoader = internalScope.Container.Container.Resolve<IServiceScopeLoader>();
            var globalScope = await scopeLoader.LoadGlobal(internalScope);

            await SceneManager.UnloadSceneAsync(startScene);

            var gamePlayLoader = globalScope.Get<IGamePlayLoader>();
            await gamePlayLoader.Initialize(globalScope);
            profiler.Dispose();
        }
    }
}