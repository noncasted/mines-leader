using Cysharp.Threading.Tasks;
using Internal;
using Menu.Common;
using VContainer;

namespace Tools
{
    public class MenuMock : MockBase
    {
        public override async UniTaskVoid Process()
        {
            var global = await Bootstrap();

            var scopeLoaderFactory = global.Container.Container.Resolve<IServiceScopeLoader>();
            
            var menuResult = await scopeLoaderFactory.LoadMenuMock(global);
            var main = menuResult.Container.Container.Resolve<IMenuLoop>();
            main.Process(menuResult.Lifetime).Forget(); 
        }
    }
}