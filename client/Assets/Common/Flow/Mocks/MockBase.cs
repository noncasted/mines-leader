using Cysharp.Threading.Tasks;
using Global.Setup;
using Internal;
using Meta;

namespace Flow.Mocks
{
    public abstract class MockBase : MockEntry
    {
        protected async UniTask<ILoadedScope> Bootstrap()
        {
            GameProfiler.Begin("Mock");

            var internalScope = await new ServiceScopeLoader().LoadInternal();
            internalScope.Container.Resolve<IStartupAssetsPreload>().Start();
            var scopeLoader = internalScope.Container.Resolve<IServiceScopeLoader>();

            var globalScope = await scopeLoader.LoadGlobal(internalScope);
            var metaScope = await scopeLoader.LoadMeta(globalScope);

            return metaScope;
        }
    }
}