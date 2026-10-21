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
        IBackendClient Client { get; }
        INetworkConnection Connection { get; }
        IReadOnlyLifetime Lifetime { get; }

        // Opens the socket and authorizes with its first frame (S3). A refusal throws MetaAuthRejectedException (the
        // server closes the socket with SharedBackendSocketAuth.RejectedCloseCode after it).
        UniTask<SharedBackendSocketAuth.Response> Connect(IReadOnlyLifetime lifetime, SharedBackendSocketAuth.Request auth);
    }

    // The server refused the saved credentials: every next attempt with them is refused the same way.
    public class MetaAuthRejectedException : Exception
    {
        public MetaAuthRejectedException(SharedBackendSocketAuth.AuthStatus status)
            : base($"[Meta] The backend refused the authorization: {status}")
        {
            Status = status;
        }

        public SharedBackendSocketAuth.AuthStatus Status { get; }
    }

    public class MetaBackend : IMetaBackend
    {
        public MetaBackend(
            NetworkConnection connection,
            IBackendClient client,
            IReadOnlyLifetime lifetime,
            BackendOptions options)
        {
            _options = options;
            _connection = connection;
            Client = client;
            Lifetime = lifetime;
        }

        private readonly BackendOptions _options;
        private readonly NetworkConnection _connection;

        public IBackendClient Client { get; }
        public INetworkConnection Connection => _connection;
        public IReadOnlyLifetime Lifetime { get; }

        /// <summary>
        /// Nothing of the credentials goes into the url (it ends up in the logs of every proxy): the request is the
        /// first frame, sent right after the socket opens, nothing else is sent before its response.
        /// </summary>
        public async UniTask<SharedBackendSocketAuth.Response> Connect(
            IReadOnlyLifetime lifetime,
            SharedBackendSocketAuth.Request auth)
        {
            Debug.Log("[Meta] Connecting to backend as: " + (auth.GuestId == Guid.Empty ? "new guest" : $"guest {auth.GuestId}"));

            // Коннект идёт параллельно загрузке меню: отрезок на стек не встаёт, иначе чужие этапы
            // вложились бы в ожидание сокета. Текущим он делается только на синхронном прологе,
            // чтобы замер самого сокета лёг внутрь.
            using var socket = GameProfiler.Concurrent("Socket");

            UniTask run;

            using (GameProfiler.Ambient(socket))
                run = _connection.Run(lifetime, _options.SocketUrl);

            await run;

            var response = await _connection.Request<SharedBackendSocketAuth.Response>(auth);

            if (response.Status != SharedBackendSocketAuth.AuthStatus.Ok)
                throw new MetaAuthRejectedException(response.Status);

            return response;
        }
    }
}
