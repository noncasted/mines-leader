using Cysharp.Threading.Tasks;
using Internal;
using Meta;

namespace Menu.Loop
{
    public class MenuLoop : IMenuLoop
    {
        public MenuLoop(
            IMenuPlay play,
            IMenuSocialLoop socialLoop)
        {
            _play = play;
            _socialLoop = socialLoop;
        }

        private readonly IMenuPlay _play;
        private readonly IMenuSocialLoop _socialLoop;

        private IReadOnlyLifetime _lifetime;

        public async UniTask<MenuResult> Process(IReadOnlyLifetime lifetime)
        {
            var completion = new UniTaskCompletionSource<SessionData>();

            await _socialLoop.Start(lifetime);
            
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