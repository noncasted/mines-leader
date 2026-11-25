using Cysharp.Threading.Tasks;
using Global.Cameras;
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

            // As GameStartup: the input of a scene converts the pointer through the current camera from its first update.
            globalScope.Container.Resolve<IGlobalCamera>().Enable();

            var metaScope = await scopeLoader.LoadMeta(globalScope);

            return metaScope;
        }
    }
}