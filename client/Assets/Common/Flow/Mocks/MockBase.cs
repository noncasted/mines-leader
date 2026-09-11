using Cysharp.Threading.Tasks;
using Flow.Loop;
using Flow.Startup;
using Global.Setup;
using Internal;
using Meta;
using UnityEngine;

namespace Flow.Mocks
{
    [DisallowMultipleComponent]
    public abstract class MockBase : MonoBehaviour
    {
        private ILoadedScope _internalScope;

        public abstract UniTaskVoid Process();

        protected async UniTask<ILoadedScope> Bootstrap()
        {
            GameProfiler.Begin("Mock");

            UnionInitializer.Execute();
            var internalScopeLoader = new InternalScopeLoader();
            _internalScope = await internalScopeLoader.Load();
            _internalScope.Container.Resolve<IStartupAssetsPreload>().Start();
            var scopeLoader = _internalScope.Container.Resolve<IServiceScopeLoader>();

            var globalScope = await scopeLoader.LoadGlobal(_internalScope);
            var metaScope = await scopeLoader.LoadMeta(globalScope);

            return metaScope;
        }

        private void OnApplicationQuit()
        {
            _internalScope?.Dispose().Forget();
        }
    }
}