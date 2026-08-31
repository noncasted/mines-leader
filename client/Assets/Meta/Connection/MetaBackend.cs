using System;
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

        UniTask Connect(IReadOnlyLifetime lifetime, Guid? userId);
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

        /// <summary>
        /// Поднимает сокет и авторизуется тем же запросом: id уезжает в query апгрейда.
        /// <paramref name="userId"/> = null означает, что сохранённого юзера нет — сервер
        /// заведёт нового, и его id приедет в профильной проекции.
        /// </summary>
        public async UniTask Connect(IReadOnlyLifetime lifetime, Guid? userId)
        {
            Debug.Log("[Meta] Connecting to backend as: " + userId);

            var url = BuildUrl(userId);

            using (GameProfiler.Scope("Socket"))
                await _connection.Run(lifetime, url);
        }

        private string BuildUrl(Guid? userId)
        {
            if (userId.HasValue == false)
                return _options.SocketUrl;

            return $"{_options.SocketUrl}?{SharedBackendSocketAuth.UserIdQueryKey}={userId.Value}";
        }
    }
}