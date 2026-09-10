using Cysharp.Threading.Tasks;
using Internal;
using Menu.Common;

namespace Flow.Mocks
{
    public class MenuMock : MockBase
    {
        public override async UniTaskVoid Process()
        {
            var global = await Bootstrap();

            var scopeLoaderFactory = global.Container.Resolve<IServiceScopeLoader>();

            var menuResult = await scopeLoaderFactory.LoadMenuMock(global);

            GameProfiler.Finish();

            var main = menuResult.Container.Resolve<IMenuLoop>();
            main.Process(menuResult.Lifetime).Forget();
        }
    }
}