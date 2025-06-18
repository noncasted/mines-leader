using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Cameras;
using Global.UI;
using Menu.Common;
using VContainer;

namespace Loop
{
    public interface IGamePlayLoader
    {
        UniTask Load(MenuResult menuResult);
    }

    public class GamePlayLoader : IGamePlayLoader
    {
        public GamePlayLoader(
            IGameLoopScopeLoader scopeLoader,
            IGlobalCamera globalCamera,
            ILoadingScreen loadingScreen)
        {
            _scopeLoader = scopeLoader;
            _globalCamera = globalCamera;
            _loadingScreen = loadingScreen;
        }

        private readonly IGameLoopScopeLoader _scopeLoader;
        private readonly IGlobalCamera _globalCamera;
        private readonly ILoadingScreen _loadingScreen;

        public async UniTask Load(MenuResult menuResult)
        {
            await _loadingScreen.Show();
            _globalCamera.Enable();
            var scope = await _scopeLoader.Load(PvPScopeExtensions.LoadPvp);
            var loop = scope.Container.Container.Resolve<IPvPGameLoop>();
            await loop.Process(scope.Lifetime, menuResult.SessionData);
        }
    }
}