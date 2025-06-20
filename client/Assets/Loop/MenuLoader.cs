using Cysharp.Threading.Tasks;
using Global.Cameras;
using Menu.Common;
using VContainer;

namespace Loop
{
    public interface IMenuLoader
    {
        UniTask<MenuResult> Load();
    }

    public class MenuLoader : IMenuLoader
    {
        public MenuLoader(IGameLoopScopeLoader scopeLoader, IGlobalCamera globalCamera)
        {
            _scopeLoader = scopeLoader;
            _globalCamera = globalCamera;
        }
        
        private readonly IGameLoopScopeLoader _scopeLoader;
        private readonly IGlobalCamera _globalCamera;
        
        public async UniTask<MenuResult> Load()
        {
            _globalCamera.Enable();
            var scope = await _scopeLoader.Load(MenuScopeExtensions.LoadMenu);
            var loop = scope.Container.Container.Resolve<IMenuLoop>();
            _globalCamera.Disable();
            
            var result = await loop.Process(scope.Lifetime);
            
            return result;
        }
    }
}