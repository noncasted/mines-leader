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
        UniTask<IGameEndTransition> Load(GameLoadData gameLoadData);
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

        public async UniTask<IGameEndTransition> Load(GameLoadData gameLoadData)
        {
            await _loadingScreen.Show();
            _globalCamera.Enable();
            
            var scope = await _scopeLoader.Load((loader, parent) => loader.LoadPvp(parent, gameLoadData.Result));
            var loop = scope.Container.Container.Resolve<IPvPGameLoop>();
            var transitionData = await loop.Process(scope.Lifetime, gameLoadData.Result);

            return transitionData;
        }
    }
}