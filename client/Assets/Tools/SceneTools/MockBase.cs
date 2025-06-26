using Cysharp.Threading.Tasks;
using Global.Setup;
using Internal;
using Meta;
using UnityEngine;
using VContainer;

namespace Tools
{
    [DisallowMultipleComponent]
    public abstract class MockBase : MonoBehaviour
    {
        private ILoadedScope _internalScope;

        public abstract UniTaskVoid Process();

        protected async UniTask<ILoadedScope> Bootstrap()
        {
            var internalConfig = AssetsExtensions.Environment.GetAsset<InternalScopeConfig>();
            var internalScopeLoader = new InternalScopeLoader(internalConfig);
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