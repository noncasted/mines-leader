using Cysharp.Threading.Tasks;
using Internal;

namespace Meta
{
    public class MetaLoop : IScopeBaseSetupAsync
    {
        public MetaLoop(
            IAuthentication authentication,
            IMetaBackend backend,
            IBackendProjectionHub projectionHub,
            IUser user)
        {
            _authentication = authentication;
            _backend = backend;
            _projectionHub = projectionHub;
            _user = user;
        }

        private readonly IAuthentication _authentication;
        private readonly IMetaBackend _backend;
        private readonly IBackendProjectionHub _projectionHub;
        private readonly IUser _user;

        public async UniTask OnBaseSetupAsync(IReadOnlyLifetime lifetime)
        {
            var userId = await _authentication.Execute();
            _user.Init(userId);

            await _backend.Connect(lifetime);
            await _projectionHub.Start(lifetime);
        }
    }
}