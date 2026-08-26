using Cysharp.Threading.Tasks;
using Global.Cameras;
using Global.UI;
using Internal;
using Menu.Play;
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
            IMenuPlay play)
        {
            _globalCamera = globalCamera;
            _loadingScreen = loadingScreen;
            _play = play;
        }

        private readonly IGlobalCamera _globalCamera;
        private readonly ILoadingScreen _loadingScreen;
        private readonly IMenuPlay _play;

        private IReadOnlyLifetime _lifetime;

        public async UniTask<GameLoadData> Process(IReadOnlyLifetime lifetime)
        {
            var completion = new UniTaskCompletionSource<SharedMatchmaking.MatchResult>();

            _loadingScreen.Hide();
            _globalCamera.Disable();

            _play.MatchFound.Advise(lifetime, sessionData => completion.TrySetResult(sessionData));

            var result = await completion.Task;

            return new GameLoadData()
            {
                Result = result
            };
        }
    }
}