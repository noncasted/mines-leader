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
        private void Awake()
        {
            Setup().Forget();
        }

        private async UniTask Setup()
        {
            // Трасса старта закрывается на входе в меню: там загрузка и заканчивается,
            // а сюда управление уже не возвращается (см. MenuLoader).
            GameProfiler.Begin("Startup");

            var internalScopeLoader = new InternalScopeLoader();

            UnionInitializer.Execute();

            var internalScope = await internalScopeLoader.Load();
            var scopeLoader = internalScope.Resolve<IServiceScopeLoader>();

            var globalScope = await scopeLoader.LoadGlobal(internalScope);
            var globalCamera = globalScope.Resolve<IGlobalCamera>();
            var loadingScreen = globalScope.Resolve<ILoadingScreen>();

            using (GameProfiler.Scope("Loading screen"))
            {
                globalCamera.Enable();
                loadingScreen.ShowInstantly();
            }

            var metaScope = await scopeLoader.LoadMeta(globalScope);

            await scopeLoader.LoadGameLoop(metaScope);
        }
    }
}