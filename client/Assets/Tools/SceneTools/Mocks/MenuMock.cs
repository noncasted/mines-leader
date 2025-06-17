using System;
using Assets.Meta;
using Cysharp.Threading.Tasks;
using Global.Backend;
using Global.GameServices;
using Internal;
using Menu;
using Menu.Loop;
using Menu.Setup;
using VContainer;

namespace Tools
{
    public class MenuMock : MockBase
    {
        public override async UniTaskVoid Process()
        {
            var global = await BootstrapGlobal();

            var scopeLoaderFactory = global.Container.Container.Resolve<IServiceScopeLoader>();

            var userContext = global.Get<IUserContext>();
            var backendHub = global.Get<IBackendProjectionHub>();
            var backendUser = global.Get<IBackendUser>();
            
            await userContext.Init(global.Lifetime);
            await backendHub.Start(global.Lifetime, userContext.Id);

            await UniTask.WaitUntil(() => backendUser.Id != Guid.Empty);
            
            var menuResult = await scopeLoaderFactory.LoadMenuMock(global);
            var main = menuResult.Container.Container.Resolve<IMenuLoop>();
            main.Process(menuResult.Lifetime).Forget(); 
        }
    }
}