using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;
using Shared;
using UnityEngine;

namespace Meta
{
    public interface IMetaBackend
    {
        IUser User { get; }
        IBackendClient Client { get; }
        INetworkSocket Socket { get; }
        IReadOnlyLifetime Lifetime { get; }

        UniTask Connect(IReadOnlyLifetime lifetime);
    }

    public class MetaBackend : IMetaBackend
    {
        public MetaBackend(
            IUser user,
            NetworkSocket socket,
            IBackendClient client,
            IReadOnlyLifetime lifetime,
            BackendOptions options)
        {
            
            _options = options;
            _socket = socket;
            User = user;
            Client = client;
            Lifetime = lifetime;
        }

        private readonly BackendOptions _options;
        private readonly NetworkSocket _socket;

        public IUser User { get; }
        public IBackendClient Client { get; }
        public INetworkSocket Socket => _socket;
        public IReadOnlyLifetime Lifetime { get; }

        public async UniTask Connect(IReadOnlyLifetime lifetime)
        {
            await _socket.Run(lifetime, _options.SocketUrl);

            var authRequest = new BackendConnectionAuth.Request()
            {
                UserId = User.Id
            };
            
            var authResponse = await _socket.SendFull<BackendConnectionAuth.Response>(authRequest);

            if (authResponse.IsSuccess == false)
                Debug.LogError($"[Projection] Failed to authenticate");
        }
    }
}