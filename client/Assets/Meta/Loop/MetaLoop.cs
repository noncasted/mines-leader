using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;

namespace Meta
{
    public class MetaLoop : IScopeBaseSetupAsync
    {
        public MetaLoop(
            IAuthentication authentication,
            IBackendProjectionHub projectionHub,
            IUser user)
        {
            _authentication = authentication;
            _projectionHub = projectionHub;
            _user = user;
        }

        private readonly IAuthentication _authentication;
        private readonly IBackendProjectionHub _projectionHub;
        private readonly IUser _user;

        public async UniTask OnBaseSetupAsync(IReadOnlyLifetime lifetime)
        {
            var userId = await _authentication.Execute();
            _user.Init(userId);
            await _projectionHub.Start(lifetime, userId);
        }
    }
}