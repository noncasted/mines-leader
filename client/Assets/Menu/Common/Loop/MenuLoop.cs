using Cysharp.Threading.Tasks;
using Global.Cameras;
using Global.UI;
using Internal;
using Menu.Main;
using Menu.Social;
using Meta;

namespace Menu.Common
{
    public interface IMenuLoop
    {
        UniTask<MenuResult> Process(IReadOnlyLifetime lifetime);
    }
    
    public class MenuLoop : IMenuLoop
    {
        public MenuLoop(
            IGlobalCamera globalCamera,
            ILoadingScreen loadingScreen,
            IMenuPlay play,
            IMenuSocialLoop socialLoop)
        {
            _globalCamera = globalCamera;
            _loadingScreen = loadingScreen;
            _play = play;
            _socialLoop = socialLoop;
        }

        private readonly IGlobalCamera _globalCamera;
        private readonly ILoadingScreen _loadingScreen;
        private readonly IMenuPlay _play;
        private readonly IMenuSocialLoop _socialLoop;

        private IReadOnlyLifetime _lifetime;

        public async UniTask<MenuResult> Process(IReadOnlyLifetime lifetime)
        {
            var completion = new UniTaskCompletionSource<SessionData>();

            await _socialLoop.Start(lifetime);
            
            _loadingScreen.Hide();
            _globalCamera.Disable();
            
            _play.GameFound.Advise(lifetime, sessionData => completion.TrySetResult(sessionData));

            var sessionData = await completion.Task;
            
            return new MenuResult()
            {
                GameMode = GameMode.PvP,
                SessionData = sessionData
            };
        }
    }
}