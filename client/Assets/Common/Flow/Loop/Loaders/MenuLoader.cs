using Cysharp.Threading.Tasks;
using Internal;
using Menu.Common;

namespace Flow.Loop
{
    public interface IMenuLoader
    {
        UniTask<GameLoadData> Load();
    }

    public class MenuLoader : IMenuLoader
    {
        public MenuLoader(IGameLoopScopeLoader scopeLoader)
        {
            _scopeLoader = scopeLoader;
        }

        private readonly IGameLoopScopeLoader _scopeLoader;

        public async UniTask<GameLoadData> Load()
        {
            // На старте меню грузится веткой, отпущенной из GameLoop, поэтому этап
            // кладётся в корень трассы. При возврате из матча трассы нет — открываем свою.
            if (GameProfiler.IsRunning == false)
                GameProfiler.Begin("Menu");

            ILoadedScope scope;

            using (GameProfiler.Branch("Menu load"))
                scope = await _scopeLoader.Load(MenuScopeExtensions.LoadMenu);

            // Меню загружено и дальше ждёт игрока: замерять больше нечего.
            GameProfiler.Finish();

            var loop = scope.Container.Resolve<IMenuLoop>();
            var result = await loop.Process(scope.Lifetime);

            return result;
        }
    }
}