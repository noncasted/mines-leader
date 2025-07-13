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
        INetworkConnection Connection { get; }
        IReadOnlyLifetime Lifetime { get; }

        UniTask Connect(IReadOnlyLifetime lifetime);
    }

    public class MetaBackend : IMetaBackend
    {
        public MetaBackend(
            IUser user,
            NetworkConnection connection,
            IBackendClient client,
            IReadOnlyLifetime lifetime,
            BackendOptions options)
        {
            
            _options = options;
            _connection = connection;
            User = user;
            Client = client;
            Lifetime = lifetime;
        }

        private readonly BackendOptions _options;
        private readonly NetworkConnection _connection;

        public IUser User { get; }
        public IBackendClient Client { get; }
        public INetworkConnection Connection => _connection;
        public IReadOnlyLifetime Lifetime { get; }

        public async UniTask Connect(IReadOnlyLifetime lifetime)
        {
            await _connection.Run(lifetime, _options.SocketUrl);

            var authRequest = new BackendConnectionAuth.Request()
            {
                UserId = User.Id
            };
            
            var authResponse = await _connection.SendFull<BackendConnectionAuth.Response>(authRequest);

            if (authResponse.IsSuccess == false)
                Debug.LogError($"[Projection] Failed to authenticate");
        }
    }
}