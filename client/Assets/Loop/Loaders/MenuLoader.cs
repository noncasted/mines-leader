using Cysharp.Threading.Tasks;
using Global.Cameras;
using Global.UI;
using Menu.Common;
using VContainer;

namespace Loop
{
    public interface IMenuLoader
    {
        UniTask<GameLoadData> Load();
    }

    public class MenuLoader : IMenuLoader
    {
        public MenuLoader(IGameLoopScopeLoader scopeLoader, IGlobalCamera globalCamera, ILoadingScreen loadingScreen)
        {
            _scopeLoader = scopeLoader;
            _globalCamera = globalCamera;
            _loadingScreen = loadingScreen;
        }
        
        private readonly IGameLoopScopeLoader _scopeLoader;
        private readonly IGlobalCamera _globalCamera;
        private readonly ILoadingScreen _loadingScreen;

        public async UniTask<GameLoadData> Load()
        {
            _globalCamera.Enable();
            _loadingScreen.Show();

            var scope = await _scopeLoader.Load(MenuScopeExtensions.LoadMenu);
            var loop = scope.Container.Container.Resolve<IMenuLoop>();
            var result = await loop.Process(scope.Lifetime);
            
            return result;
        }
    }
}