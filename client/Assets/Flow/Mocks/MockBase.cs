using Cysharp.Threading.Tasks;
using Global.Setup;
using Internal;
using Meta;
using UnityEngine;
using VContainer;

namespace Flow
{
    [DisallowMultipleComponent]
    public abstract class MockBase : MonoBehaviour
    {
        private ILoadedScope _internalScope;

        public abstract UniTaskVoid Process();

        protected async UniTask<ILoadedScope> Bootstrap()
        {
            var assets = AssetsExtensions.FindAsset<AssetsStorage>();
            assets.Cache();
            var internalScopeLoader = new InternalScopeLoader(assets);
            _internalScope = internalScopeLoader.Load();
            var scopeLoader = _internalScope.Container.Container.Resolve<IServiceScopeLoader>();

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