using Cysharp.Threading.Tasks;
using Global.Cameras;
using Global.UI;
using Internal;
using Menu.Common;
using VContainer;

namespace Flow.Loop
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
            // На старте меню грузится веткой, отпущенной из GameLoop, поэтому этап
            // кладётся в корень трассы. При возврате из матча трассы нет — открываем свою.
            if (GameProfiler.IsRunning == false)
                GameProfiler.Begin("Menu");

            ILoadedScope scope;

            using (GameProfiler.Branch("Menu load"))
            {
                _globalCamera.Enable();

                using (GameProfiler.Scope("Loading screen"))
                    await _loadingScreen.Show();

                scope = await _scopeLoader.Load(MenuScopeExtensions.LoadMenu);
            }

            // Меню загружено и дальше ждёт игрока: замерять больше нечего.
            GameProfiler.Finish();

            var loop = scope.Container.Container.Resolve<IMenuLoop>();
            var result = await loop.Process(scope.Lifetime);

            return result;
        }
    }
}