using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Cameras;
using Global.UI;
using Internal;
using Menu.Common;

namespace Flow.Loop
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
            // Своя трасса на переход из меню в матч: трасса старта к этому моменту закрыта.
            GameProfiler.Begin("GamePlay");

            ILoadedScope scope;

            using (GameProfiler.Scope("Load"))
            {
                using (GameProfiler.Scope("Loading screen"))
                    await _loadingScreen.Show();

                _globalCamera.Enable();

                scope = await _scopeLoader.Load((loader, parent) => loader.LoadPvp(parent, gameLoadData.Result));
            }

            // Дальше начинается сам матч, а он живёт вне загрузки.
            GameProfiler.Finish();

            var loop = scope.Container.Resolve<IGamePlayLoop>();
            var transitionData = await loop.Process(scope.Lifetime, gameLoadData.Result);

            return transitionData;
        }
    }
}