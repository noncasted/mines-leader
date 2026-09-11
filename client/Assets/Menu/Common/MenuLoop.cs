using Cysharp.Threading.Tasks;
using Global.Cameras;
using Global.UI;
using Internal;
using Menu.Play;
using Meta;
using Shared;

namespace Menu.Common
{
    public interface IMenuLoop
    {
        UniTask<GameLoadData> Process(IReadOnlyLifetime lifetime);
    }

    public class MenuLoop : IMenuLoop
    {
        public MenuLoop(
            IGlobalCamera globalCamera,
            ILoadingScreen loadingScreen,
            IMenuPlay play,
            IBackendProjectionsAwaiter projections,
            IEventLoop eventLoop)
        {
            _globalCamera = globalCamera;
            _loadingScreen = loadingScreen;
            _play = play;
            _projections = projections;
            _eventLoop = eventLoop;
        }

        private readonly IGlobalCamera _globalCamera;
        private readonly ILoadingScreen _loadingScreen;
        private readonly IMenuPlay _play;
        private readonly IBackendProjectionsAwaiter _projections;
        private readonly IEventLoop _eventLoop;

        public async UniTask<GameLoadData> Process(IReadOnlyLifetime lifetime)
        {
            var completion = new UniTaskCompletionSource<SharedMatchmaking.MatchResult>();

            // Меню грузится параллельно с метой: авторизация и проекции могли ещё не приехать.
            // Отрезок параллельный — ветка подключения меты в это время ещё пишет свои замеры.
            await GameProfiler.Concurrent("Wait meta").Track(_projections.IsInitialized.WaitTrue(lifetime));

            using (GameProfiler.Scope("Meta setup completed"))
                _eventLoop.RunCustom<IMetaSetupCompleted>(lifetime, l => l.OnMetaSetupCompleted(lifetime));

            _loadingScreen.Hide();
            _globalCamera.Disable();

            // Меню достроено по данным меты и дальше ждёт игрока: замерять больше нечего.
            GameProfiler.Finish();

            _play.MatchFound.Advise(lifetime, sessionData => completion.TrySetResult(sessionData));

            var result = await completion.Task;

            return new GameLoadData()
            {
                Result = result
            };
        }
    }
}
