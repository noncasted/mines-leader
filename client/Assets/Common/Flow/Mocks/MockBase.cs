using Cysharp.Threading.Tasks;
using Flow.Loop;
using Global.Setup;
using Internal;
using Meta;
using UnityEngine;

namespace Flow.Mocks
{
    [DisallowMultipleComponent]
    public abstract class MockBase : MonoBehaviour
    {
        public abstract UniTaskVoid Process();

        protected async UniTask<ILoadedScope> Bootstrap()
        {
            GameProfiler.Begin("Mock");

            UnionInitializer.Execute();
            var internalScope = await new ServiceScopeLoader().LoadInternal();
            internalScope.Container.Resolve<IStartupAssetsPreload>().Start();
            var scopeLoader = internalScope.Container.Resolve<IServiceScopeLoader>();

            var globalScope = await scopeLoader.LoadGlobal(internalScope);
            var metaScope = await scopeLoader.LoadMeta(globalScope);

            return metaScope;
        }
    }
}