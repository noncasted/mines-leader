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
            // On startup the menu loads as a branch released from GameLoop, so the stage
            // goes to the trace root. When returning from a match there is no trace - open our own.
            if (GameProfiler.IsRunning == false)
                GameProfiler.Begin("Menu");

            // The stage of the trace is "Menu", opened by LoadMenu itself: a stage here would be a second root with the same time.
            var scope = await _scopeLoader.Load(MenuScopeExtensions.LoadMenu);

            // The trace is closed in MenuLoop: the menu still waits for the meta data, which loads
            // in parallel, and only then removes the loading screen.
            var loop = scope.Container.Resolve<IMenuLoop>();
            var result = await loop.Process(scope.Lifetime);

            return result;
        }
    }
}