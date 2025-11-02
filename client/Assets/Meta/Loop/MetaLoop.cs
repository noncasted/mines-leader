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
            IUser user,
            IMetaConnectionAwaiter connectionAwaiter)
        {
            _authentication = authentication;
            _backend = backend;
            _user = user;
            _connectionAwaiter = connectionAwaiter;
        }

        private readonly IAuthentication _authentication;
        private readonly IMetaBackend _backend;
        private readonly IUser _user;
        private readonly IMetaConnectionAwaiter _connectionAwaiter;

        public async UniTask OnBaseSetupAsync(IReadOnlyLifetime lifetime)
        {
            Debug.Log("[Meta] [Loop] Starting meta loop initialization");

            Debug.Log("[Meta] [Loop] Executing authentication");
            var userId = await _authentication.Execute();
            Debug.Log("[Meta] [Loop] User authenticated: " + userId);

            Debug.Log("[Meta] [Loop] Initializing user with ID: " + userId);
            _user.Init(userId);

            Debug.Log("[Meta] [Loop] Connecting to backend");
            var connectionLifetime = lifetime.Child();
            var isSuccess = await _backend.Connect(connectionLifetime);

            if (isSuccess == false)
            {
                connectionLifetime.Terminate();
                Debug.Log("[Meta] [Loop] Backend connection failed, signing up new user");
                var response = await _backend.SignUp("HUESOS");
                PlayerPrefs.SetString("userId", response.Id.ToString());
                _user.Init(response.Id);
                await _backend.Connect(lifetime);
            }

            Debug.Log("[Meta] [Loop] Backend connection established");

            Debug.Log("[Meta] [Loop] Waiting for connection completion");
            await _connectionAwaiter.CompleteTask;
            Debug.Log("[Meta] [Loop] Meta loop initialization completed successfully");
        }
    }
}