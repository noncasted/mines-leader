using Cysharp.Threading.Tasks;
using Global.Audio;
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
            IMetaState metaState,
            IEventLoop eventLoop,
            IAudioPlayer audioPlayer)
        {
            _globalCamera = globalCamera;
            _loadingScreen = loadingScreen;
            _play = play;
            _metaState = metaState;
            _eventLoop = eventLoop;
            _audioPlayer = audioPlayer;
        }

        private readonly IGlobalCamera _globalCamera;
        private readonly ILoadingScreen _loadingScreen;
        private readonly IMenuPlay _play;
        private readonly IMetaState _metaState;
        private readonly IEventLoop _eventLoop;
        private readonly IAudioPlayer _audioPlayer;

        public async UniTask<GameLoadData> Process(IReadOnlyLifetime lifetime)
        {
            var completion = new UniTaskCompletionSource<SharedMatchmaking.MatchResult>();

            // Меню грузится параллельно с метой: авторизация, проекции и реестры могли ещё не успеть.
            // Отрезок параллельный — ветки меты в это время ещё пишут свои замеры.
            await GameProfiler.Concurrent("Wait meta").Track(_metaState.IsReady.WaitTrue(lifetime));

            using (GameProfiler.Scope("Meta setup completed"))
                _eventLoop.RunCustom<IMetaSetupCompleted>(lifetime, l => l.OnMetaSetupCompleted(lifetime));

            _loadingScreen.Hide();
            _globalCamera.Disable();
            _audioPlayer.PlayLoopMusic(GlobalAudio.GameMusic);

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
