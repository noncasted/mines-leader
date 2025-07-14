using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Meta
{
    public class MetaLoop : IScopeBaseSetupAsync
    {
        public MetaLoop(
            IAuthentication authentication,
            IMetaBackend backend,
            IUser user)
        {
            _authentication = authentication;
            _backend = backend;
            _user = user;
        }

        private readonly IAuthentication _authentication;
        private readonly IMetaBackend _backend;
        private readonly IUser _user;

        public async UniTask OnBaseSetupAsync(IReadOnlyLifetime lifetime)
        {
            var userId = await _authentication.Execute();
            _user.Init(userId);
            Debug.Log("[Meta] User authenticated: " + userId);
            await _backend.Connect(lifetime);
        }
    }
}