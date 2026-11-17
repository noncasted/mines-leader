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

            // The stage of the trace is "Menu", opened by LoadMenu itself: a stage here would be a second root with the same time.
            var scope = await _scopeLoader.Load(MenuScopeExtensions.LoadMenu);

            // Трасса закрывается в MenuLoop: меню ещё дожидается данных меты, которые грузятся
            // параллельно, и только потом снимает экран загрузки.
            var loop = scope.Container.Resolve<IMenuLoop>();
            var result = await loop.Process(scope.Lifetime);

            return result;
        }
    }
}